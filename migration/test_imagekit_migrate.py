import json
import tempfile
import unittest
from pathlib import Path

from imagekit_migrate import (
    AssetPlan,
    Journal,
    ManifestRow,
    MigrationRunner,
    build_asset_plan,
    ensure_raw_mpo_url,
)


class FakeStorageClient:
    def __init__(self, fail_on_upload_number=None):
        self.fail_on_upload_number = fail_on_upload_number
        self.upload_calls = []
        self.delete_calls = []
        self.download_calls = []

    def upload(self, file_bytes, file_name, folder, content_type):
        self.upload_calls.append((file_name, folder, content_type, len(file_bytes)))
        if self.fail_on_upload_number and len(self.upload_calls) == self.fail_on_upload_number:
            raise RuntimeError("boom")
        file_id = "file-%d" % len(self.upload_calls)
        return {"fileId": file_id, "filePath": "%s/%s" % (folder, file_name), "url": "https://example.test/%s/%s" % (folder.strip("/"), file_name)}

    def delete(self, file_id):
        self.delete_calls.append(file_id)
        return {"deleted": True, "not_found": False}

    def build_delivery_url(self, file_path):
        return "https://ik.example.test%s" % file_path

    def download(self, url):
        self.download_calls.append(url)
        original_name = Path(url.split("?")[0]).name
        if original_name.endswith(".MPO"):
            return (self._original_root / ("Picture/" + original_name)).read_bytes()
        return b""


class UrlTests(unittest.TestCase):
    def test_mpo_url_adds_orig_true(self):
        self.assertEqual(
            ensure_raw_mpo_url("https://ik.example.test/path/1.MPO"),
            "https://ik.example.test/path/1.MPO?tr=orig-true",
        )

    def test_mpo_url_merges_existing_transformations(self):
        self.assertEqual(
            ensure_raw_mpo_url("https://ik.example.test/path/1.mpo?tr=w-300,h-200&v=1#frag"),
            "https://ik.example.test/path/1.mpo?tr=orig-true%2Cw-300%2Ch-200&v=1#frag",
        )

    def test_non_mpo_url_is_unchanged(self):
        self.assertEqual(
            ensure_raw_mpo_url("https://ik.example.test/path/1.JPG?tr=w-100"),
            "https://ik.example.test/path/1.JPG?tr=w-100",
        )

    def test_mpo_url_deduplicates_transformations(self):
        self.assertEqual(
            ensure_raw_mpo_url("https://ik.example.test/path/1.mpo?tr=w-300,w-300,orig-true"),
            "https://ik.example.test/path/1.mpo?tr=w-300%2Corig-true",
        )


class MigrationRunnerTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        root = Path(self.temp_dir.name)
        (root / "Picture").mkdir()
        (root / "Picture/1.MPO").write_bytes(b"stereo-bytes")
        (root / "Picture/1.JPG").write_bytes(b"preview-bytes")
        (root / "Picture/1-thumb_sm.JPG").write_bytes(b"sm")
        (root / "Picture/1-thumb_md.JPG").write_bytes(b"md")
        self.root = root
        self.output = root / "out"

    def tearDown(self):
        self.temp_dir.cleanup()

    def test_build_asset_plan_detects_existing_assets(self):
        plan = build_asset_plan(ManifestRow(1, 2, "Picture/1.MPO", "3D"), self.root)
        self.assertTrue(plan.original_path.name.endswith("1.MPO"))
        self.assertTrue(plan.preview_path.name.endswith("1.JPG"))
        self.assertTrue(plan.thumb_small_path.name.endswith("thumb_sm.JPG"))
        self.assertTrue(plan.thumb_medium_path.name.endswith("thumb_md.JPG"))

    def test_dry_run_does_not_upload(self):
        journal = Journal(self.output / "journal.jsonl")
        client = FakeStorageClient()
        runner = MigrationRunner(client, self.root, journal, self.output, True, 2, 10)
        result = runner.run([ManifestRow(1, 2, "Picture/1.MPO", "3D")])
        self.assertEqual(result["dry_run"], 1)
        self.assertEqual(client.upload_calls, [])

    def test_resume_skips_successful_rows(self):
        journal = Journal(self.output / "journal.jsonl")
        journal.append({"picture_id": 1, "gallery_id": 2, "status": "success", "message": "ok", "assets": {}, "sql": None, "timestamp": "now"})
        client = FakeStorageClient()
        runner = MigrationRunner(client, self.root, journal, self.output, True, 1, 10)
        result = runner.run([ManifestRow(1, 2, "Picture/1.MPO", "3D")])
        self.assertEqual(result["skipped"], 1)
        self.assertEqual(client.upload_calls, [])

    def test_failed_upload_cleans_up_uploaded_files(self):
        journal = Journal(self.output / "journal.jsonl")
        client = FakeStorageClient(fail_on_upload_number=2)
        runner = MigrationRunner(client, self.root, journal, self.output, False, 1, 10)
        client._original_root = self.root
        result = runner.run([ManifestRow(1, 2, "Picture/1.MPO", "3D")])
        self.assertEqual(result["error"], 1)
        self.assertEqual(client.delete_calls, ["file-1"])

    def test_success_writes_sql_and_verifies_raw_mpo_url(self):
        journal = Journal(self.output / "journal.jsonl")
        client = FakeStorageClient()
        client._original_root = self.root
        runner = MigrationRunner(client, self.root, journal, self.output, False, 1, 10)
        result = runner.run([ManifestRow(1, 2, "Picture/1.MPO", "3D")])
        self.assertEqual(result["success"], 1)
        sql_files = list(self.output.glob("batch_*.sql"))
        self.assertEqual(len(sql_files), 1)
        sql_text = sql_files[0].read_text(encoding="utf-8")
        self.assertIn("StorageMigrationStatus", sql_text)
        self.assertTrue(client.download_calls[0].endswith("?tr=orig-true"))

    def test_duplicate_ids_in_same_batch_are_deduplicated(self):
        journal = Journal(self.output / "journal.jsonl")
        client = FakeStorageClient()
        runner = MigrationRunner(client, self.root, journal, self.output, True, 4, 10)
        result = runner.run([
            ManifestRow(1, 2, "Picture/1.MPO", "3D"),
            ManifestRow(1, 2, "Picture/1.MPO", "3D"),
        ])
        self.assertEqual(result["dry_run"], 1)


if __name__ == "__main__":
    unittest.main()
