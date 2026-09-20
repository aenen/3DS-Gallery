#!/usr/bin/env python3
import argparse
import base64
import concurrent.futures
import csv
from datetime import UTC, datetime
import hashlib
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, List, Optional

IMAGEKIT_DEFAULT_FOLDER = "/3dsgallery/pictures"
STORAGE_PROVIDER = "ImageKit"
STATUS_REMOTE_ACTIVE = "RemoteActive"
JOURNAL_SUCCESS = "success"


@dataclass
class ManifestRow:
    picture_id: int
    gallery_id: Optional[int]
    path: str
    picture_type: str
    creation_date: str = ""
    description: str = ""


@dataclass
class AssetPlan:
    original_path: Path
    preview_path: Path
    thumb_small_path: Optional[Path]
    thumb_medium_path: Optional[Path]
    remote_legacy_path: str
    picture_type: str


class ImageKitStorageClient:
    def __init__(self, private_key: str, endpoint: str, upload_folder: str = IMAGEKIT_DEFAULT_FOLDER, timeout: int = 30, max_retries: int = 3):
        if not private_key:
            raise ValueError("ImageKit private key is required")
        if not endpoint:
            raise ValueError("ImageKit endpoint is required")
        self.private_key = private_key
        self.endpoint = endpoint.rstrip("/")
        self.upload_folder = normalize_folder(upload_folder)
        self.timeout = timeout
        self.max_retries = max(1, max_retries)

    def upload(self, file_bytes: bytes, file_name: str, folder: str, content_type: str) -> Dict[str, object]:
        fields = {
            "fileName": file_name,
            "folder": normalize_folder(folder),
            "useUniqueFileName": "false",
            "overwriteFile": "true",
            "overwriteAITags": "true",
            "overwriteTags": "true",
            "overwriteCustomMetadata": "true",
        }
        boundary = "----ImageKitBoundary%s" % int(time.time() * 1000)
        body = build_multipart_body(boundary, fields, file_bytes, file_name, content_type)
        request = urllib.request.Request(
            url="https://upload.imagekit.io/api/v1/files/upload",
            data=body,
            headers={
                "Authorization": build_basic_auth(self.private_key),
                "Content-Type": "multipart/form-data; boundary=%s" % boundary,
            },
            method="POST",
        )
        return self._request_json(request)

    def delete(self, file_id: str) -> Dict[str, object]:
        request = urllib.request.Request(
            url="https://api.imagekit.io/v1/files/%s" % urllib.parse.quote(file_id),
            headers={"Authorization": build_basic_auth(self.private_key)},
            method="DELETE",
        )
        try:
            self._request_json(request)
            return {"deleted": True, "not_found": False}
        except urllib.error.HTTPError as error:
            if error.code == 404:
                return {"deleted": False, "not_found": True}
            raise

    def download(self, url: str) -> bytes:
        request = urllib.request.Request(url=url, method="GET")
        return self._request_bytes(request)

    def build_delivery_url(self, file_path: str) -> str:
        normalized = file_path.replace("\\", "/")
        if not normalized.startswith("/"):
            normalized = "/" + normalized
        return self.endpoint + normalized

    def _request_json(self, request: urllib.request.Request) -> Dict[str, object]:
        payload = self._request_bytes(request)
        return json.loads(payload.decode("utf-8"))

    def _request_bytes(self, request: urllib.request.Request) -> bytes:
        last_error = None
        for attempt in range(1, self.max_retries + 1):
            try:
                with urllib.request.urlopen(request, timeout=self.timeout) as response:
                    return response.read()
            except urllib.error.HTTPError as error:
                last_error = error
                if error.code not in (408, 429) and error.code < 500:
                    raise
                if attempt >= self.max_retries:
                    raise
                time.sleep(get_retry_delay_seconds(error, attempt))
            except urllib.error.URLError as error:
                last_error = error
                if attempt >= self.max_retries:
                    raise
                time.sleep(get_retry_delay_seconds(None, attempt))
        raise last_error


class Journal:
    def __init__(self, journal_path: Path):
        self.journal_path = journal_path
        self._entries_by_id = {}
        if journal_path.exists():
            with journal_path.open("r", encoding="utf-8") as handle:
                for line in handle:
                    line = line.strip()
                    if not line:
                        continue
                    entry = json.loads(line)
                    self._entries_by_id[entry["picture_id"]] = entry

    def get(self, picture_id: int) -> Optional[Dict[str, object]]:
        return self._entries_by_id.get(picture_id)

    def append(self, entry: Dict[str, object]) -> None:
        self.journal_path.parent.mkdir(parents=True, exist_ok=True)
        with self.journal_path.open("a", encoding="utf-8") as handle:
            handle.write(json.dumps(entry, sort_keys=True) + "\n")
        self._entries_by_id[entry["picture_id"]] = entry


class MigrationRunner:
    def __init__(self, storage_client, picture_directory: Path, journal: Journal, output_directory: Path, dry_run: bool, max_workers: int, batch_size: int):
        self.storage_client = storage_client
        self.picture_directory = picture_directory
        self.journal = journal
        self.output_directory = output_directory
        self.dry_run = dry_run
        self.max_workers = max(1, max_workers)
        self.batch_size = max(1, batch_size)

    def run(self, manifest_rows: List[ManifestRow]) -> Dict[str, int]:
        counts = {"success": 0, "skipped": 0, "missing": 0, "error": 0, "dry_run": 0}
        manifest_rows = dedupe_manifest_rows(manifest_rows)
        for chunk_index, chunk in enumerate(chunked(manifest_rows, self.batch_size), start=1):
            with concurrent.futures.ThreadPoolExecutor(max_workers=self.max_workers) as executor:
                futures = [executor.submit(self.process_row, row) for row in chunk]
                results = [future.result() for future in futures]

            sql_lines = []
            for result in results:
                counts[result["status"]] = counts.get(result["status"], 0) + 1
                self.journal.append(result)
                if result.get("sql"):
                    sql_lines.append(result["sql"])

            state = {
                "chunk": chunk_index,
                "processed": sum(counts.values()),
                "counts": counts,
                "completed_at": utc_now_iso(),
            }
            self.output_directory.mkdir(parents=True, exist_ok=True)
            with (self.output_directory / "state.json").open("w", encoding="utf-8") as handle:
                json.dump(state, handle, indent=2, sort_keys=True)

            if sql_lines:
                sql_file = self.output_directory / ("batch_%03d.sql" % chunk_index)
                with sql_file.open("w", encoding="utf-8") as handle:
                    handle.write("\n".join(sql_lines) + "\n")

        return counts

    def process_row(self, row: ManifestRow) -> Dict[str, object]:
        previous = self.journal.get(row.picture_id)
        if previous and previous.get("status") == JOURNAL_SUCCESS:
            return self._entry(row, "skipped", message="already successful in journal")

        plan = build_asset_plan(row, self.picture_directory)
        if not plan:
            return self._entry(row, "missing", message="original file not found")

        if self.dry_run:
            assets = describe_plan(plan)
            return self._entry(row, "dry_run", assets=assets, message="dry run only")

        uploaded = []
        try:
            original = self.storage_client.upload(
                plan.original_path.read_bytes(),
                plan.original_path.name,
                IMAGEKIT_DEFAULT_FOLDER + "/originals",
                guess_content_type(plan.original_path),
            )
            uploaded.append(original["fileId"])

            preview = None
            if plan.preview_path.resolve() == plan.original_path.resolve() and plan.picture_type.upper() == "2D":
                preview = original
            else:
                preview = self.storage_client.upload(
                    plan.preview_path.read_bytes(),
                    plan.preview_path.name,
                    IMAGEKIT_DEFAULT_FOLDER + "/previews",
                    "image/jpeg",
                )
                uploaded.append(preview["fileId"])

            thumb_small = None
            if plan.thumb_small_path and plan.thumb_small_path.exists():
                thumb_small = self.storage_client.upload(
                    plan.thumb_small_path.read_bytes(),
                    plan.thumb_small_path.name,
                    IMAGEKIT_DEFAULT_FOLDER + "/thumbs/sm",
                    "image/jpeg",
                )
                uploaded.append(thumb_small["fileId"])

            thumb_medium = None
            if plan.thumb_medium_path and plan.thumb_medium_path.exists():
                thumb_medium = self.storage_client.upload(
                    plan.thumb_medium_path.read_bytes(),
                    plan.thumb_medium_path.name,
                    IMAGEKIT_DEFAULT_FOLDER + "/thumbs/md",
                    "image/jpeg",
                )
                uploaded.append(thumb_medium["fileId"])

            original_bytes = plan.original_path.read_bytes()
            remote_original_url = ensure_raw_mpo_url(self.storage_client.build_delivery_url(original["filePath"]))
            remote_original_bytes = self.storage_client.download(remote_original_url)
            if len(remote_original_bytes) != len(original_bytes) or sha256_hex(remote_original_bytes) != sha256_hex(original_bytes):
                raise RuntimeError("verification failed for original asset")

            sql = build_update_sql(row, plan, original, preview, thumb_small, thumb_medium)
            return self._entry(
                row,
                JOURNAL_SUCCESS,
                sql=sql,
                assets={
                    "original": original,
                    "preview": preview,
                    "thumb_small": thumb_small,
                    "thumb_medium": thumb_medium,
                },
                message="uploaded and verified",
            )
        except Exception as error:
            for file_id in uploaded:
                try:
                    self.storage_client.delete(file_id)
                except Exception:
                    pass
            return self._entry(row, "error", message=str(error))

    def _entry(self, row: ManifestRow, status: str, message: str, assets: Optional[Dict[str, object]] = None, sql: Optional[str] = None) -> Dict[str, object]:
        return {
            "picture_id": row.picture_id,
            "gallery_id": row.gallery_id,
            "status": status,
            "message": message,
            "assets": assets or {},
            "sql": sql,
            "timestamp": utc_now_iso(),
        }


def build_asset_plan(row: ManifestRow, picture_directory: Path) -> Optional[AssetPlan]:
    original = picture_directory / row.path.replace("\\", "/")
    if not original.exists():
        return None

    preview = original if row.picture_type.upper() == "2D" else picture_directory / ("Picture/%s.JPG" % row.picture_id)
    thumb_small = picture_directory / ("Picture/%s-thumb_sm.JPG" % row.picture_id)
    thumb_medium = picture_directory / ("Picture/%s-thumb_md.JPG" % row.picture_id)
    return AssetPlan(
        original_path=original,
        preview_path=preview if preview.exists() else original,
        thumb_small_path=thumb_small if thumb_small.exists() else None,
        thumb_medium_path=thumb_medium if thumb_medium.exists() else None,
        remote_legacy_path=row.path.replace("\\", "/"),
        picture_type=row.picture_type,
    )


def describe_plan(plan: AssetPlan) -> Dict[str, Optional[str]]:
    return {
        "original": str(plan.original_path),
        "preview": str(plan.preview_path),
        "thumb_small": str(plan.thumb_small_path) if plan.thumb_small_path else None,
        "thumb_medium": str(plan.thumb_medium_path) if plan.thumb_medium_path else None,
    }


def build_update_sql(row: ManifestRow, plan: AssetPlan, original: Dict[str, object], preview: Dict[str, object], thumb_small: Optional[Dict[str, object]], thumb_medium: Optional[Dict[str, object]]) -> str:
    values = {
        "StorageProvider": STORAGE_PROVIDER,
        "StorageMigrationStatus": STATUS_REMOTE_ACTIVE,
        "OriginalRemoteFileId": original["fileId"],
        "OriginalRemotePath": original["filePath"],
        "PreviewRemoteFileId": preview["fileId"],
        "PreviewRemotePath": preview["filePath"],
        "ThumbnailSmallRemoteFileId": thumb_small["fileId"] if thumb_small else None,
        "ThumbnailSmallRemotePath": thumb_small["filePath"] if thumb_small else None,
        "ThumbnailMediumRemoteFileId": thumb_medium["fileId"] if thumb_medium else None,
        "ThumbnailMediumRemotePath": thumb_medium["filePath"] if thumb_medium else None,
    }
    assignments = []
    for key, value in values.items():
        if value is None:
            assignments.append("[%s] = NULL" % key)
        else:
            assignments.append("[%s] = '%s'" % (key, sql_escape(str(value))))
    assignment_sql = ", ".join(assignments)
    return (
        "MERGE [dbo].[PictureRemoteAsset] AS target "
        "USING (SELECT %d AS [PictureId]) AS source "
        "ON target.[PictureId] = source.[PictureId] "
        "WHEN MATCHED THEN UPDATE SET %s "
        "WHEN NOT MATCHED THEN INSERT ([PictureId], [StorageProvider], [StorageMigrationStatus], [OriginalRemoteFileId], [OriginalRemotePath], [PreviewRemoteFileId], [PreviewRemotePath], [ThumbnailSmallRemoteFileId], [ThumbnailSmallRemotePath], [ThumbnailMediumRemoteFileId], [ThumbnailMediumRemotePath]) "
        "VALUES (%d, '%s', '%s', '%s', '%s', '%s', '%s', %s, %s, %s, %s);"
    ) % (
        row.picture_id,
        assignment_sql,
        row.picture_id,
        sql_escape(str(values["StorageProvider"])),
        sql_escape(str(values["StorageMigrationStatus"])),
        sql_escape(str(values["OriginalRemoteFileId"])),
        sql_escape(str(values["OriginalRemotePath"])),
        sql_escape(str(values["PreviewRemoteFileId"])),
        sql_escape(str(values["PreviewRemotePath"])),
        "NULL" if values["ThumbnailSmallRemoteFileId"] is None else "'%s'" % sql_escape(str(values["ThumbnailSmallRemoteFileId"])),
        "NULL" if values["ThumbnailSmallRemotePath"] is None else "'%s'" % sql_escape(str(values["ThumbnailSmallRemotePath"])),
        "NULL" if values["ThumbnailMediumRemoteFileId"] is None else "'%s'" % sql_escape(str(values["ThumbnailMediumRemoteFileId"])),
        "NULL" if values["ThumbnailMediumRemotePath"] is None else "'%s'" % sql_escape(str(values["ThumbnailMediumRemotePath"])),
    )


def guess_content_type(path: Path) -> str:
    suffix = path.suffix.lower()
    if suffix == ".mpo":
        return "image/mpo"
    if suffix in (".jpg", ".jpeg"):
        return "image/jpeg"
    return "application/octet-stream"


def ensure_raw_mpo_url(url: str) -> str:
    if not url:
        return url
    parsed = urllib.parse.urlsplit(url)
    if not parsed.path.lower().endswith(".mpo"):
        return url

    query_items = urllib.parse.parse_qsl(parsed.query, keep_blank_values=True)
    transformations = []
    non_transform = []
    for key, value in query_items:
        if key.lower() == "tr":
            transformations.extend([item.strip() for item in value.split(",") if item.strip()])
        else:
            non_transform.append((key, value))

    seen = set()
    merged = []
    if not any(item.lower() == "orig-true" for item in transformations):
        transformations.insert(0, "orig-true")
    for item in transformations:
        lower = item.lower()
        if lower in seen:
            continue
        seen.add(lower)
        merged.append(item)

    rebuilt_query = [("tr", ",".join(merged))] + non_transform
    return urllib.parse.urlunsplit((parsed.scheme, parsed.netloc, parsed.path, urllib.parse.urlencode(rebuilt_query), parsed.fragment))


def normalize_folder(folder: str) -> str:
    folder = (folder or IMAGEKIT_DEFAULT_FOLDER).replace("\\", "/").strip()
    if not folder.startswith("/"):
        folder = "/" + folder
    return folder.rstrip("/")


def build_basic_auth(private_key: str) -> str:
    token = base64.b64encode((private_key + ":").encode("ascii")).decode("ascii")
    return "Basic " + token


def build_multipart_body(boundary: str, fields: Dict[str, str], file_bytes: bytes, file_name: str, content_type: str) -> bytes:
    parts = []
    for key, value in fields.items():
        parts.append(("--%s\r\n" % boundary).encode("utf-8"))
        parts.append(("Content-Disposition: form-data; name=\"%s\"\r\n\r\n%s\r\n" % (key, value)).encode("utf-8"))
    parts.append(("--%s\r\n" % boundary).encode("utf-8"))
    parts.append(("Content-Disposition: form-data; name=\"file\"; filename=\"%s\"\r\n" % file_name).encode("utf-8"))
    parts.append(("Content-Type: %s\r\n\r\n" % content_type).encode("utf-8"))
    parts.append(file_bytes)
    parts.append(("\r\n--%s--\r\n" % boundary).encode("utf-8"))
    return b"".join(parts)


def get_retry_delay_seconds(error: Optional[urllib.error.HTTPError], attempt: int) -> int:
    if error is not None:
        retry_after = error.headers.get("Retry-After")
        if retry_after and retry_after.isdigit():
            return min(int(retry_after), 10)
    return min(attempt * attempt, 10)


def sha256_hex(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sql_escape(value: str) -> str:
    return value.replace("'", "''")


def read_manifest(path: Path) -> List[ManifestRow]:
    rows = []
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        for record in reader:
            rows.append(ManifestRow(
                picture_id=int(record["id"]),
                gallery_id=int(record["galleryId"]) if record.get("galleryId") else None,
                path=record["path"],
                picture_type=(record.get("type") or "").strip() or "2D",
                creation_date=record.get("creationDate", ""),
                description=record.get("description", ""),
            ))
    return rows


def chunked(rows: List[ManifestRow], size: int) -> Iterable[List[ManifestRow]]:
    for index in range(0, len(rows), size):
        yield rows[index:index + size]


def dedupe_manifest_rows(rows: List[ManifestRow]) -> List[ManifestRow]:
    unique_rows = []
    seen_ids = set()
    for row in rows:
        if row.picture_id in seen_ids:
            continue
        seen_ids.add(row.picture_id)
        unique_rows.append(row)
    return unique_rows


def utc_now_iso() -> str:
    return datetime.now(UTC).isoformat().replace("+00:00", "Z")


def parse_args(argv: List[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Migrate 3DS Gallery picture assets to ImageKit using a CSV manifest.")
    parser.add_argument("--manifest", required=True, help="CSV export with at least id,galleryId,path,type columns")
    parser.add_argument("--picture-root", required=True, help="Absolute path to the repository root or deployed site root that contains the Picture directory")
    parser.add_argument("--output-dir", required=True, help="Directory for journal, state, and SQL update batches")
    parser.add_argument("--dry-run", action="store_true", help="Inventory and verify inputs without uploading or writing SQL updates")
    parser.add_argument("--batch-size", type=int, default=50)
    parser.add_argument("--max-workers", type=int, default=4)
    parser.add_argument("--imagekit-private-key", default=os.environ.get("ImageKitPrivateKey", ""))
    parser.add_argument("--imagekit-endpoint", default=os.environ.get("ImageKitUrlEndpoint", ""))
    parser.add_argument("--imagekit-upload-folder", default=os.environ.get("ImageKitUploadFolder", IMAGEKIT_DEFAULT_FOLDER))
    return parser.parse_args(argv)


def main(argv: List[str]) -> int:
    args = parse_args(argv)
    manifest_path = Path(args.manifest)
    picture_root = Path(args.picture_root)
    output_directory = Path(args.output_dir)
    journal = Journal(output_directory / "journal.jsonl")
    manifest_rows = read_manifest(manifest_path)

    if args.dry_run:
        storage_client = None
    else:
        storage_client = ImageKitStorageClient(
            private_key=args.imagekit_private_key,
            endpoint=args.imagekit_endpoint,
            upload_folder=args.imagekit_upload_folder,
        )

    runner = MigrationRunner(
        storage_client=storage_client,
        picture_directory=picture_root,
        journal=journal,
        output_directory=output_directory,
        dry_run=args.dry_run,
        max_workers=args.max_workers,
        batch_size=args.batch_size,
    )
    counts = runner.run(manifest_rows)
    print(json.dumps(counts, sort_keys=True))
    return 0 if counts.get("error", 0) == 0 else 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
