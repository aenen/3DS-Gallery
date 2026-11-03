# 3DS Gallery – Cloudinary Migration Guide

This guide explains how to migrate all existing pictures (previously stored as
local files on the web server) to Cloudinary, and how to update the database so
that the `path` column for each picture contains a valid Cloudinary `public_id`
instead of a legacy file-system path.

After completing this guide the application will work entirely through Cloudinary
and no legacy fallback code will be needed.

The Python scripts referenced in Step 2 live alongside this README in the
`migration/` folder.  Run them from the directory that contains the `Picture/`
folder (the web-server root, or wherever you extracted the backup).


---

## Overview of the Legacy Storage Format

Legacy pictures were saved with paths like:

```
Picture\{id}.JPG          (2D picture, stored under the web root)
Picture\{id}.MPO          (3D picture original)
```

A picture is identified as **legacy** when its `path` column value has a file
extension (`.JPG`, `.MPO`).

A picture is identified as **Cloudinary** when `path` has **no** file extension –
it is a Cloudinary `public_id` of the form: `Picture/{id}`

After migration every row should have a Cloudinary `public_id` in `path`.


---

## Prerequisites

1. A Cloudinary account with the following credentials:
   - Cloud Name  (`CloudinaryCloudName` in `Web.config`)
   - API Key     (`CloudinaryApiKey`    in `Web.config`)
   - API Secret  (`CloudinaryApiSecret` in `Web.config`)

2. Access to the production SQL Server database.

3. Access to the web server's file system where the legacy `Picture/` directory
   lives (or a full backup/copy of that directory).

4. Python 3.8+ and the Cloudinary CLI:
   ```
   pip install cloudinary-cli
   ```


---

## Step 1 – Identify Legacy Pictures in the Database

Run this SQL query to find all pictures that still use legacy paths:

```sql
SELECT id, path, type
FROM   Picture
WHERE  path LIKE '%.JPG'
   OR  path LIKE '%.MPO'
   OR  path LIKE '%.jpg'
   OR  path LIKE '%.mpo';
```

If you want a complete export to a temp table:

```sql
SELECT id, path, type
INTO   #LegacyPictures
FROM   Picture
WHERE  path LIKE '%.%';   -- any extension
```


---

## Step 2 – Bulk Upload Legacy Files to Cloudinary (~4 000 pictures)

Because there are thousands of files and the Cloudinary web interface does not
support raw MPO uploads, all uploads are performed via the Cloudinary CLI
(`cld`) and the helper scripts in this folder.

For each legacy picture you must upload:

| Picture type | Asset | `public_id` |
|---|---|---|
| 2D | `.JPG` file | `Picture/{id}` |
| 3D | Left-eye JPEG (from `.MPO`) | `Picture/{id}` |
| 3D | Right-eye JPEG (from `.MPO`) | `Picture/{id}_r` |
| 3D | Original `.MPO` as **raw** resource | `Picture/{id}_mpo` |

> **Note on MPO files:** An MPO file is two concatenated JPEGs.  The scripts
> split on the second JPEG SOI marker (`0xFF 0xD8`) to extract the two eye
> images.

### Sub-step 2a – Configure the Cloudinary CLI

```bash
# Install
pip install cloudinary-cli

# Configure credentials (stored in ~/.cloudinary)
cld config --cloud_name <cloud_name> \
            --api_key    <api_key>    \
            --api_secret <api_secret>

# Verify
cld whoami
```

### Sub-step 2b – Split MPO files into left/right JPEGs

Run from the directory that contains `Picture/`:

```bash
python migration/split_mpo.py
```

This creates `Picture/{id}_left.jpg` and `Picture/{id}_right.jpg` for every
`.MPO` file.

### Sub-step 2c – Bulk upload 2D JPEGs and left-eye frames

```bash
cld uploader upload_dir Picture/ \
    --resource-type image         \
    --folder ""                   \
    --public-id-prefix Picture    \
    --unique-filename false       \
    --use-filename true           \
    --overwrite true              \
    --include-hidden false        \
    --exclude "*.MPO" --exclude "*.mpo" --exclude "*_right.jpg"
```

This uploads:
- `Picture/12345.JPG`       → `public_id = Picture/12345`       (2D)
- `Picture/12345_left.jpg`  → `public_id = Picture/12345_left`  (renamed in 2f)

> `upload_dir` requires cloudinary-cli ≥ 3.0.  Confirm with `cld --version`.

### Sub-step 2d – Upload right-eye JPEGs with `_r` suffix

```bash
python migration/upload_right_eyes.py
```

Uploads each `*_right.jpg` file with `public_id = Picture/{id}_r`.

### Sub-step 2e – Upload original MPO files as raw resources

```bash
python migration/upload_mpo_raw.py
```

Uploads each `.MPO` file as `resource_type=raw` with
`public_id = Picture/{id}_mpo` (no file extension).

### Sub-step 2f – Rename left-eye uploads in Cloudinary

```bash
python migration/rename_left_eyes.py
```

Renames `Picture/{id}_left` → `Picture/{id}` in Cloudinary so the public_id
matches what the application expects.

> **Alternative:** To skip this rename step, edit `split_mpo.py` to save the
> left-eye file as `{id}.jpg` instead of `{id}_left.jpg`, then remove the
> `*_left.jpg` exclusion concern from step 2c.


---

## Step 3 – Update the Database `path` Column

After every picture has been successfully uploaded to Cloudinary, update the
database so the `path` column holds the Cloudinary `public_id`.

```sql
-- Backup first
SELECT id, path
INTO   Picture_path_backup_YYYYMMDD
FROM   Picture;

-- Update all legacy rows
UPDATE Picture
SET    path = CONCAT('Picture/', CAST(id AS VARCHAR(20)))
WHERE  path LIKE '%.%';    -- rows that still have a file extension
```

**Verification** – after the update, no row should have an extension:

```sql
SELECT COUNT(*)
FROM   Picture
WHERE  path LIKE '%.%';    -- should return 0
```

**Rollback** (if needed):

```sql
UPDATE p
SET    p.path = b.path
FROM   Picture p
JOIN   Picture_path_backup_YYYYMMDD b ON b.id = p.id;
```


---

## Step 4 – Verify the Migration

1. Browse the live site and spot-check several pictures (2D and 3D) to confirm
   they load from Cloudinary URLs (`res.cloudinary.com/...`).

2. Test a 3D picture download on a real Nintendo 3DS console to confirm the
   MPO file is served correctly from the `OriginalMpo` action.

3. Test the delete flow for at least one 2D and one 3D picture to confirm that
   the Cloudinary assets are removed.

4. Check the home page, gallery preview tiles, and user profile page to ensure
   thumbnails load correctly.


---

## Step 5 – Clean Up Old Local Files (Optional)

Once all pictures are loading from Cloudinary and the database is fully updated,
you may remove the legacy `Picture/` directory from the web server.

> **WARNING:** Do NOT delete local files until you have confirmed:
> - Every picture row in the database has been updated (Step 3 verification).
> - All Cloudinary uploads completed successfully (check script output / logs).
> - The live site shows no broken images.

Keep a compressed archive backup of `Picture/` for at least 30 days before
permanently deleting it.


---

## Summary Checklist

- [ ] 1. Identify all legacy picture rows in the database (Step 1).
- [ ] 2. Configure Cloudinary CLI (`cld config …`).
- [ ] 3. Split MPO files: `python migration/split_mpo.py`
- [ ] 4. Bulk upload 2D JPEGs + left-eye frames: `cld uploader upload_dir …`
- [ ] 5. Upload right-eye JPEGs: `python migration/upload_right_eyes.py`
- [ ] 6. Upload MPO raw files: `python migration/upload_mpo_raw.py`
- [ ] 7. Rename left-eye assets in Cloudinary: `python migration/rename_left_eyes.py`
- [ ] 8. Update `path` column in database (Step 3 SQL).
- [ ] 9. Verify – confirm no legacy paths remain and the site works correctly.
- [ ] 10. (Optional) Archive and remove the legacy `Picture/` directory.
