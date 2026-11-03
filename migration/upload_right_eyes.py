"""
upload_right_eyes.py – Step 2d of the Cloudinary migration.

Uploads every *_right.jpg file from the Picture/ directory to Cloudinary as an
image resource.  The Cloudinary public_id is set to "Picture/<id>_r" so it
matches the naming convention expected by the application.

Requires the Cloudinary CLI (pip install cloudinary-cli) and credentials
configured via:
    cld config --cloud_name <name> --api_key <key> --api_secret <secret>

Run from the directory that contains the Picture/ folder:
    python upload_right_eyes.py

Adjust MAX_WORKERS to control concurrency.  The Cloudinary free tier allows
roughly 10 requests/s; paid plans support higher throughput.
"""

import os
import subprocess
from concurrent.futures import ThreadPoolExecutor, as_completed

PICTURE_DIR = "Picture"
MAX_WORKERS = 8


def upload(fname):
    pic_id = fname.replace("_right.jpg", "")   # e.g. "12345"
    public_id = f"Picture/{pic_id}_r"
    fpath = os.path.join(PICTURE_DIR, fname)
    result = subprocess.run(
        [
            "cld", "uploader", "upload", fpath,
            "--resource-type", "image",
            "--public-id", public_id,
            "--overwrite", "true",
        ],
        capture_output=True,
        text=True,
    )
    return fname, result.returncode, result.stderr.strip()


files = [f for f in os.listdir(PICTURE_DIR) if f.endswith("_right.jpg")]

if not files:
    print("No *_right.jpg files found in Picture/. Run split_mpo.py first.")
else:
    print(f"Uploading {len(files)} right-eye images with {MAX_WORKERS} workers …")
    ok = err = 0
    with ThreadPoolExecutor(max_workers=MAX_WORKERS) as pool:
        futures = {pool.submit(upload, f): f for f in files}
        for fut in as_completed(futures):
            fname, rc, stderr = fut.result()
            if rc == 0:
                print(f"OK  {fname}")
                ok += 1
            else:
                print(f"ERR {fname} – {stderr}")
                err += 1
    print(f"\nDone. {ok} uploaded, {err} errors.")
