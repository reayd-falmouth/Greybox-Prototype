# Third-party vendor zip for CI (matches .github/actions/setup-thirdparty-assets).
# Run from this directory (greybox-prototype repo root). Requires: zip, aws (CLI v2).
#
#   make                  # default: zip + upload (same as publish)
#   make help             # list targets
#   make zip | upload | publish | clean
#
# Examples:
#   AWS_PROFILE=stones make
#   make upload S3_BUCKET=my-bucket S3_KEY=greybox-3rdparty.zip

.DEFAULT_GOAL := publish

.PHONY: help zip upload publish clean check-zip check-aws

# Output matches CI default object key; artifact lives under build/ (gitignored).
ZIP_NAME      ?= thirdparty-ci.zip
ZIP_PATH      ?= build/$(ZIP_NAME)
ASSETS_DIR    := Unity/Assets
THIRDPARTY    := $(ASSETS_DIR)/3rdParty

AWS_REGION    ?= eu-west-1
S3_BUCKET     ?= greybox-prototype-unity-3rdparty-assets-eu-west-1-093581297635
S3_KEY        ?= $(ZIP_NAME)

S3_URI        := s3://$(S3_BUCKET)/$(S3_KEY)

help:
	@echo "Third-party zip for CI (contents unzip into Unity/Assets)."
	@echo ""
	@echo "  make zip       Create $(ZIP_PATH) from $(THIRDPARTY)"
	@echo "  make upload    aws s3 cp $(ZIP_PATH) $(S3_URI)"
	@echo "  make publish   zip and upload"
	@echo "  make clean     remove $(ZIP_PATH)"
	@echo ""
	@echo "Overrides: ZIP_NAME, ZIP_PATH, AWS_REGION, S3_BUCKET, S3_KEY"
	@echo "Requires: zip, aws CLI (configure AWS_ACCESS_KEY_ID / AWS_SECRET_ACCESS_KEY or profiles)"

check-zip:
	@command -v zip >/dev/null 2>&1 || (echo "error: zip not found (install or use Git Bash on Windows)" && exit 1)

check-aws:
	@command -v aws >/dev/null 2>&1 || (echo "error: aws CLI not found" && exit 1)

check-thirdparty:
	@test -d "$(THIRDPARTY)" || (echo "error: missing $(THIRDPARTY) — restore vendor assets locally first" && exit 1)

$(ZIP_PATH): check-zip check-thirdparty
	mkdir -p "$(dir $(ZIP_PATH))"
	rm -f "$(ZIP_PATH)"
	cd "$(ASSETS_DIR)" && zip -r -q "../../$(ZIP_PATH)" 3rdParty

zip: $(ZIP_PATH)
	@echo "Wrote $(ZIP_PATH) (zip root contains 3rdParty/ for CI unzip into Unity/Assets)"

upload: check-aws $(ZIP_PATH)
	aws s3 cp "$(ZIP_PATH)" "$(S3_URI)" --region "$(AWS_REGION)"

publish: zip upload
	@echo "Uploaded $(S3_URI)"

clean:
	rm -f "$(ZIP_PATH)"
	@rmdir build 2>/dev/null || true
