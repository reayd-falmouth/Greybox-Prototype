# CI Artifacts in S3

This repository uses dual-write CI artifact publishing:

- Build jobs upload artifacts to GitHub Artifacts (temporary fallback).
- Build jobs also upload platform bundles to S3 under `ci-artifacts/`.
- Release jobs resolve and download artifacts from S3 first.

## S3 key contract

Base prefix:

- `ci-artifacts/{repository}/{run_id}/{platform}/`

Objects:

- `bundle.tar.gz` - compressed build output
- `metadata.json` - metadata (`sha`, `run_id`, `ref`, `workflow`, `platform`)

Examples:

- `ci-artifacts/reayd-falmouth/Greybox-Prototype/24393540910/Windows/bundle.tar.gz`
- `ci-artifacts/reayd-falmouth/Greybox-Prototype/24393540910/iOS/metadata.json`

## Release artifact resolution

In `release.yml` manual dispatch:

- `source_run_id` (preferred) - directly selects artifacts from that CI run.
- `source_build_sha` - resolves latest successful build run for that SHA.
- If both are empty, release uses artifacts from the current run.

## Lifecycle policy (14 days)

Apply a prefix-scoped lifecycle policy to avoid storage cost growth.

```bash
aws s3api put-bucket-lifecycle-configuration \
  --bucket "$S3_BUCKET" \
  --lifecycle-configuration "file://scripts/ci/s3-lifecycle-ci-artifacts.json"
```

Use the same bucket already used for third-party assets, but keep this rule restricted to `ci-artifacts/` so vendor object retention is unaffected.
