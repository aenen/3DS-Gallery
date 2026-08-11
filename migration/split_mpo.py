"""
split_mpo.py – Step 2b of the Cloudinary migration.

Splits every .MPO file in the Picture/ directory into two JPEG files:
    Picture/<id>_left.jpg   (first / left eye)
    Picture/<id>_right.jpg  (second / right eye)

Run from the directory that contains the Picture/ folder:
    python split_mpo.py
"""

import os

PICTURE_DIR = "Picture"
SOI = b"\xff\xd8"

processed = 0
errors = 0

for fname in sorted(os.listdir(PICTURE_DIR)):
    if not fname.upper().endswith(".MPO"):
        continue

    fpath = os.path.join(PICTURE_DIR, fname)
    pic_id = os.path.splitext(fname)[0]  # e.g. "12345"

    try:
        with open(fpath, "rb") as f:
            data = f.read()

        # Find the second SOI marker (skip the first one at offset 0)
        second_soi = data.index(SOI, 2)

        left_bytes = data[:second_soi]
        right_bytes = data[second_soi:]

        left_path = os.path.join(PICTURE_DIR, f"{pic_id}_left.jpg")
        right_path = os.path.join(PICTURE_DIR, f"{pic_id}_right.jpg")

        with open(left_path, "wb") as f:
            f.write(left_bytes)
        with open(right_path, "wb") as f:
            f.write(right_bytes)

        print(f"OK  {fname}: left={len(left_bytes)}B  right={len(right_bytes)}B")
        processed += 1

    except ValueError:
        print(f"ERR {fname}: second SOI marker not found – skipping")
        errors += 1
    except Exception as e:
        print(f"ERR {fname}: {e}")
        errors += 1

print(f"\nDone. {processed} split, {errors} errors.")
