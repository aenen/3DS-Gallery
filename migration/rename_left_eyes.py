"""
rename_left_eyes.py – Step 2f of the Cloudinary migration.

After the bulk upload in step 2c, left-eye images land in Cloudinary with
public_ids like "Picture/12345_left".  This script renames each of them to
"Picture/12345" (i.e. removes the _left suffix) so that the public_id matches
the naming convention expected by the application.

Requires the Cloudinary CLI (pip install cloudinary-cli) and credentials
configured via:
    cld config --cloud_name <name> --api_key <key> --api_secret <secret>

Run from the directory that contains the Picture/ folder (so the local
*_left.jpg files can be discovered):
    python rename_left_eyes.py

Adjust MAX_WORKERS to control concurrency.

ALTERNATIVE: If you prefer to skip this step entirely, edit split_mpo.py to
save left-eye files as "{id}.jpg" instead of "{id}_left.jpg", and update the
upload_dir exclude pattern in step 2c to "--exclude '*_right.jpg'" only.
"""

import os
import subprocess
from concurrent.futures import ThreadPoolExecutor, as_completed

PICTURE_DIR = "Picture"
MAX_WORKERS = 8


def rename(fname):
    pic_id = fname.replace("_left.jpg", "")   # e.g. "12345"
    old_public = f"Picture/{pic_id}_left"
    new_public = f"Picture/{pic_id}"
    result = subprocess.run(
        [
            "cld", "uploader", "rename", old_public, new_public,
            "--overwrite", "true",
        ],
        capture_output=True,
        text=True,
    )
    return fname, result.returncode, result.stderr.strip()


files = [f for f in os.listdir(PICTURE_DIR) if f.endswith("_left.jpg")]

if not files:
    print("No *_left.jpg files found in Picture/. Run split_mpo.py first.")
else:
    print(f"Renaming {len(files)} left-eye Cloudinary assets with {MAX_WORKERS} workers …")
    ok = err = 0
    with ThreadPoolExecutor(max_workers=MAX_WORKERS) as pool:
        futures = {pool.submit(rename, f): f for f in files}
        for fut in as_completed(futures):
            fname, rc, stderr = fut.result()
            if rc == 0:
                print(f"OK  {fname}  →  {fname.replace('_left.jpg', '')}")
                ok += 1
            else:
                print(f"ERR {fname} – {stderr}")
                err += 1
    print(f"\nDone. {ok} renamed, {err} errors.")
