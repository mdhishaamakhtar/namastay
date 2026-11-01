.PHONY: setup up down dev client test-e2e import import-check media-sync sample test test-unit test-integration coverage format lint lint-all check check-all

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

sample:
	dotnet run --project src/Namastay.Importer -- samples/data
	dotnet run --project src/Namastay.Importer -- --media samples/media

test: test-unit test-integration

test-unit:
	dotnet test tests/Namastay.UnitTests

test-integration:
	dotnet test tests/Namastay.IntegrationTests

# Needs `make up`, `make sample`, `make dev` and `make client` running.
test-e2e:
	dotnet test tests/Namastay.E2ETests

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

# Everything, including the browser tests. Needs the app running.
check-all: check test-e2e
