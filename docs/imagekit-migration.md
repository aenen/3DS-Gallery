# ImageKit migration guide

This repository now supports mixed local/remote picture delivery and new uploads to ImageKit.

## What changed

- New uploads persist to ImageKit instead of the local `Picture/` folder.
- Existing rows can stay local until they are migrated.
- Every remote `.MPO` open/download URL is resolved with `tr=orig-true` so ImageKit serves the original MPO bytes.
- The database now stores explicit ImageKit file IDs and remote paths in a separate `PictureRemoteAsset` table for:
  - original asset
  - preview JPG
  - optional `thumb_sm`
  - optional `thumb_md`
- `StorageProvider` and `StorageMigrationStatus` stay in `PictureRemoteAsset`, so the existing `Picture` table stays unchanged.

## Do this first

1. Back up the SQL database.
2. Back up the production `Picture/` directory independently.
3. Create an ImageKit folder structure budget that accounts for multiple assets per picture:
   - originals
   - previews
   - optional small thumbs
   - optional medium thumbs
4. Confirm ImageKit account limits and API rate limits for the migration window.
5. Plan a maintenance window or freeze deletes/uploads during the final migration pass.

## Required configuration

Set these deployment settings before enabling remote uploads in production:

- `ImageKitPublicKey`
- `ImageKitPrivateKey`
- `ImageKitUrlEndpoint` (example: `https://ik.imagekit.io/your_imagekit_id`)
- `ImageKitUploadFolder` (default in code: `/3dsgallery/pictures`)
- `ImageKitTimeoutSeconds` (default `30`)
- `ImageKitMaxRetries` (default `3`)

Do **not** commit real credentials.

## Database/schema deployment order

1. Deploy the database migration that creates `dbo.PictureRemoteAsset` with:
   - `StorageProvider`
   - `StorageMigrationStatus`
   - `OriginalRemoteFileId`
   - `OriginalRemotePath`
   - `PreviewRemoteFileId`
   - `PreviewRemotePath`
   - `ThumbnailSmallRemoteFileId`
   - `ThumbnailSmallRemotePath`
   - `ThumbnailMediumRemoteFileId`
   - `ThumbnailMediumRemotePath`
2. Deploy the application with ImageKit settings configured.
3. Verify a staging upload before migrating production history.

## Staging verification checklist

Before production migration, manually verify in staging:

1. Upload a 2D JPG.
2. Upload a 3D MPO/JPG-from-3DS.
3. Open the image normally.
4. Download JPG from the menu.
5. Download MPO from the menu.
6. Confirm the remote MPO URL contains `tr=orig-true`.
7. Open the QR/share URL.
8. Open a side-by-side render.
9. Delete the picture and confirm ImageKit assets are removed.
10. Repeat on an actual Nintendo 3DS if available.

## Migration utility

The repository includes `migration/imagekit_migrate.py`.

It is designed for:

- dry runs
- batch processing
- bounded concurrency
- JSONL journaling
- resumable reruns
- SQL update batch generation
- original-byte verification by SHA-256 and byte count using raw MPO delivery
- missing file reporting
- cleanup of partially uploaded assets when a row fails

### Input manifest

Export a CSV from production with at least these columns:

- `id`
- `galleryId`
- `path`
- `type`
- `creationDate` (recommended)
- `description` (optional)

Example SQL export source:

```sql
SELECT
    id,
    galleryId,
    path,
    type,
    creationDate,
    description
FROM dbo.Picture
ORDER BY id;
```

The `path` values must match the production `Picture/` directory layout.

## Dry run

Run the dry run first to inventory missing/corrupt inputs before uploading anything:

```bash
python3 /absolute/path/to/repo/migration/imagekit_migrate.py \
  --manifest /absolute/path/to/picture-manifest.csv \
  --picture-root /absolute/path/to/3dsGallery.WebUI \
  --output-dir /absolute/path/to/migration-output \
  --dry-run \
  --batch-size 50 \
  --max-workers 4
```

Review:

- `migration-output/journal.jsonl`
- `migration-output/state.json`

## Pilot migration

Migrate a small pilot batch first.

```bash
export ImageKitPrivateKey='***'
export ImageKitUrlEndpoint='https://ik.imagekit.io/your_imagekit_id'
export ImageKitUploadFolder='/3dsgallery/pictures'

python3 /absolute/path/to/repo/migration/imagekit_migrate.py \
  --manifest /absolute/path/to/picture-manifest.csv \
  --picture-root /absolute/path/to/3dsGallery.WebUI \
  --output-dir /absolute/path/to/migration-output/pilot \
  --batch-size 10 \
  --max-workers 2
```

This writes:

- `journal.jsonl`
- `state.json`
- one `batch_XXX.sql` file per processed batch

Apply the generated SQL only after reviewing the pilot results.

## Full migration / resume

Resume by rerunning the same command against the same output directory. Rows already marked `success` in the journal are skipped.

```bash
python3 /absolute/path/to/repo/migration/imagekit_migrate.py \
  --manifest /absolute/path/to/picture-manifest.csv \
  --picture-root /absolute/path/to/3dsGallery.WebUI \
  --output-dir /absolute/path/to/migration-output/full \
  --batch-size 50 \
  --max-workers 4
```

If a crash happens mid-run:

- do **not** delete the output directory
- inspect `journal.jsonl`
- fix the underlying problem
- rerun the same command

## Applying DB updates

The utility generates idempotent SQL upsert statements for `dbo.PictureRemoteAsset` per batch.

Recommended process:

1. Run pilot.
2. Review ImageKit files and journal.
3. Apply the pilot SQL updates.
4. Verify the pilot rows in the app.
5. Run the full migration.
6. Apply the remaining SQL updates in order.

Only rows with fully uploaded and verified assets are written as `RemoteActive`.

## Mixed-mode rollout and cutover

During rollout:

- existing local-only rows keep working
- newly uploaded rows use ImageKit
- migrated rows switch to ImageKit through `PictureRemoteAsset`

Do not delete the local `Picture/` directory during the migration.

## Post-migration checks for all ~4,000 pictures

After all SQL batches are applied:

1. Compare migrated row count against the manifest row count.
2. Count `RemoteActive` rows in `dbo.PictureRemoteAsset`.
3. Count journal `success` rows.
4. Randomly sample public 2D rows.
5. Randomly sample public 3D rows.
6. Verify `GenerateSideBySide` still works for migrated 3D rows.
7. Verify at least one private gallery manually.
8. Verify QR/share links.
9. Verify a few old local rows still resolve correctly if any remain.
10. Verify Nintendo 3DS viewing behavior on actual hardware if possible.

## Rollback boundaries

Rollback is mixed-mode safe for already-local rows because they were never switched.

For migrated rows:

- application rollback is possible because `path` still preserves the legacy relative path
- data rollback requires the pre-migration SQL backup and retained local `Picture/` files
- new uploads created after cutover are remote-only; rolling those back requires keeping their ImageKit originals or restoring from a separate export/backup

## CDN/deletion note

Deleting an ImageKit file removes the asset by file ID, but CDN caches may not disappear instantly. Do not promise immediate global disappearance after delete; verify according to ImageKit cache behavior.

## Local cleanup (later, not now)

Only after:

- all rows are verified
- staging and production manual checks pass
- independent backups are confirmed
- rollback is no longer needed

should you consider deleting legacy local assets.

Do **not** make local cleanup part of the first migration pass.
