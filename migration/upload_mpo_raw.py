"""
upload_mpo_raw.py – Step 2e of the Cloudinary migration.

Uploads every .MPO file from the Picture/ directory to Cloudinary as a RAW
resource (resource_type=raw).  The Cloudinary public_id is set to
"Picture/<id>_mpo" with NO file extension, which is what the application
expects when serving the original MPO download.

The Cloudinary web interface and image pipeline do not support MPO files, so
this CLI-based upload is required.

Requires the Cloudinary CLI (pip install cloudinary-cli) and credentials
configured via:
    cld config --cloud_name <name> --api_key <key> --api_secret <secret>

Run from the directory that contains the Picture/ folder:
    python upload_mpo_raw.py

Adjust MAX_WORKERS to control concurrency.
"""

import os
import subprocess
from concurrent.futures import ThreadPoolExecutor, as_completed

PICTURE_DIR = "Picture"
MAX_WORKERS = 8


def upload_mpo(fname):
    pic_id = os.path.splitext(fname)[0]        # strip .MPO / .mpo
    public_id = f"Picture/{pic_id}_mpo"
    fpath = os.path.join(PICTURE_DIR, fname)
    result = subprocess.run(
        [
            "cld", "uploader", "upload", fpath,
            "--resource-type", "raw",
            "--public-id", public_id,
            "--overwrite", "true",
        ],
        capture_output=True,
        text=True,
    )
    return fname, result.returncode, result.stderr.strip()


files = [f for f in os.listdir(PICTURE_DIR) if f.upper().endswith(".MPO")]

if not files:
    print("No .MPO files found in Picture/.")
else:
    print(f"Uploading {len(files)} MPO files as raw resources with {MAX_WORKERS} workers …")
    ok = err = 0
    with ThreadPoolExecutor(max_workers=MAX_WORKERS) as pool:
        futures = {pool.submit(upload_mpo, f): f for f in files}
        for fut in as_completed(futures):
            fname, rc, stderr = fut.result()
            if rc == 0:
                print(f"OK  {fname}")
                ok += 1
            else:
                print(f"ERR {fname} – {stderr}")
                err += 1
    print(f"\nDone. {ok} uploaded, {err} errors.")
