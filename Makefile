.PHONY: setup up down dev client test-all test-e2e import import-check media-sync media-prune sample test test-unit test-integration coverage format lint lint-all check check-all

LINT = dotnet dotnet-fsharplint lint --lint-config fsharplint.json

setup:
	dotnet tool restore
	git config core.hooksPath .githooks

up:
	docker compose up -d

down:
	docker compose down

dev:
	dotnet run --project src/Namastay.Web

client:
	cd src/Namastay.Client && npm install && dotnet fable . -o build --watch --run npx vite

import-check:
	dotnet run --project src/Namastay.Importer -- --dry-run data

import:
	dotnet run --project src/Namastay.Importer -- data

media-sync:
	dotnet run --project src/Namastay.Importer -- --media data/media

# Removes photos from the bucket that are gone from data/media, keeping a copy in MEDIA_BACKUP.
media-prune:
	dotnet run --project src/Namastay.Importer -- --media-prune data/media $(MEDIA_BACKUP)

sample:
	dotnet run --project src/Namastay.Importer -- samples/data
	dotnet run --project src/Namastay.Importer -- --media samples/media

# Unit and integration tests, side by side.
test:
	@$(MAKE) -j2 test-unit test-integration

# Every test there is, side by side. Needs `make up`.
# Everything builds once first, so the parallel test runs don't fight over the same files.
test-all:
	dotnet build tests/Namastay.UnitTests --verbosity quiet
	dotnet build tests/Namastay.IntegrationTests --verbosity quiet
	dotnet build tests/Namastay.E2ETests --verbosity quiet
	dotnet build src/Namastay.Web --verbosity quiet
	dotnet build src/Namastay.Importer --verbosity quiet
	@$(MAKE) -j3 test-unit test-integration test-e2e TEST_FLAGS=--no-build

test-unit:
	dotnet test tests/Namastay.UnitTests $(TEST_FLAGS) -- xUnit.StopOnFail=true

test-integration:
	dotnet test tests/Namastay.IntegrationTests $(TEST_FLAGS) -- xUnit.StopOnFail=true

# Uses its own sample database and servers, so it works whatever `make dev` has loaded. Needs `make up`.
test-e2e:
	./scripts/test-e2e.sh

coverage:
	dotnet test tests/Namastay.UnitTests /p:CollectCoverage=true /p:Include="[Namastay.Domain]*" \
		/p:Threshold=85 /p:ThresholdType=branch /p:ThresholdStat=total /p:CoverletOutputFormat=cobertura

format:
	dotnet fantomas src tests

# Changed files only, for everyday work.
lint:
	@{ git diff --name-only --diff-filter=ACM HEAD -- '*.fs'; git ls-files --others --exclude-standard '*.fs'; } \
		| xargs -n 1 -P 8 $(LINT)

# Every project in parallel.
lint-all:
	@git ls-files '*.fsproj' | xargs -n 1 -P 8 $(LINT)

check:
	dotnet fantomas --check src tests
	$(MAKE) lint-all
	$(MAKE) test-integration
	$(MAKE) coverage

# Everything, including the browser tests. Needs `make up`.
check-all: check test-e2e
