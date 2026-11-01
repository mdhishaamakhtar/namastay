# Architecture

- `Namastay.Shared` holds API contracts and typed routes. Both the server and the Fable client compile it, so it stays Fable-compatible.
- `Namastay.Domain` holds types and pure rules, with no database or web code. It references `Shared` because the trip it builds is the contract the API returns and stores, and a second copy would only drift.
- `Domain/Catalog/ContractMapping.fs` is the only place domain types become contract types. `Data/Import/Encode.fs` and `Data/Catalog/Decode.fs` are the only places they become database text.
- `Namastay.Data` owns SQL, through Dapper. Rows are private to each feature's queries, because they differ by query.
- `Namastay.Web` owns HTTP. Every route is named in `Routes.fs`, and every failure is an `ApiError`.
- The API is its own address, for the website and for native apps alike. The browser calls it directly, and CORS allows only `Site:Url`. Sign-in is one signed JWT: native apps get it from `/api/auth/token` and send it as a bearer token, and browsers hold it in an `HttpOnly` cookie.
- Photo addresses in the API are paths like `/media/...`. Each client adds the API's address, so saved trips never store a host.
- Travel data changes only through the importer, so reads are cached per import.

`make check` is the gate: formatting, full lint, all tests and the coverage minimum. Commits lint only staged files, so they stay fast.
