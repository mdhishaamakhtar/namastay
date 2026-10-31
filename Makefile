.PHONY: setup up down dev test test-unit test-integration coverage format lint check

setup:
	dotnet tool restore
	git config core.hooksPath .githooks

up:
	docker compose up -d

down:
	docker compose down

dev:
	dotnet run --project src/Namastay.Web

test: test-unit test-integration

test-unit:
	dotnet test tests/Namastay.UnitTests

test-integration:
	dotnet test tests/Namastay.IntegrationTests

coverage:
	dotnet test tests/Namastay.UnitTests /p:CollectCoverage=true /p:Include="[Namastay.Domain]*" \
		/p:Threshold=85 /p:ThresholdType=branch /p:ThresholdStat=total /p:CoverletOutputFormat=cobertura

format:
	dotnet fantomas src tests

lint:
	dotnet dotnet-fsharplint lint Namastay.sln | tee /dev/stderr | grep -q "Summary: 0 warnings"

check:
	dotnet fantomas --check src tests
	$(MAKE) lint
	$(MAKE) test-integration
	$(MAKE) coverage
