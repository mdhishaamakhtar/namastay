namespace Namastay.IntegrationTests.Auth

open System
open System.Net
open System.Net.Http
open System.Net.Http.Json
open Npgsql
open Xunit
open Namastay.Data.Accounts
open Namastay.IntegrationTests
open Namastay.IntegrationTests.Support
open Namastay.Shared
open Namastay.Shared.Contracts
open Namastay.Web.Auth
open Namastay.Web.Http

type AuthTests(postgres: PostgresFixture) =
    let scope = DatabaseScope.For postgres
    let password = "correct horse battery"

    let start () =
        TestApp.start scope Sample.catalog (fun app data -> AuthApi.map app data.Source)

    let credentials email secret : Credentials = { Email = email; Password = secret }

    let send (http: HttpClient) (url: string) (body: Credentials) =
        http.PostAsJsonAsync(url, body, Json.options)

    let signUp http email secret =
        send http Routes.signUp.Template (credentials email secret)

    let signIn http email secret =
        send http Routes.signIn.Template (credentials email secret)

    let expectError (response: HttpResponseMessage) (status: HttpStatusCode) (code: ErrorCode) =
        task {
            let! error = response.Content.ReadFromJsonAsync<ApiError>(Json.options)
            Assert.Equal(status, response.StatusCode)
            Assert.Equal(code, error.Code)
            return error
        }

    /// Reads /api/me using the session cookie a response handed back.
    let me (http: HttpClient) (response: HttpResponseMessage) =
        task {
            use request = new HttpRequestMessage(HttpMethod.Get, Routes.me.Template)

            for cookie in response.Headers.GetValues "Set-Cookie" do
                request.Headers.Add("Cookie", cookie.Split(';')[0])

            return! http.SendAsync request
        }

    let scalar (db: string) (statement: string) =
        task {
            use connection = new NpgsqlConnection(db)
            do! connection.OpenAsync()
            use command = new NpgsqlCommand(statement, connection)
            let! value = command.ExecuteScalarAsync()
            return value
        }

    let tokenFor http email =
        task {
            let! response = send http Routes.issueToken.Template (credentials email password)
            let! grant = response.Content.ReadFromJsonAsync<TokenGrant>(Json.options)
            return grant.Token
        }

    let withBearer (http: HttpClient) (token: string) =
        task {
            use request = new HttpRequestMessage(HttpMethod.Get, Routes.me.Template)
            request.Headers.Authorization <- Headers.AuthenticationHeaderValue("Bearer", token)
            let! response = http.SendAsync request
            return response.StatusCode
        }

    [<Fact>]
    member _.``sign up creates an account and signs in``() =
        task {
            let! http, _ = start ()
            let! response = signUp http "asha@example.org" password
            let! account = response.Content.ReadFromJsonAsync<Account>(Json.options)
            let! current = me http response
            Assert.Equal(HttpStatusCode.OK, response.StatusCode)
            Assert.Equal("asha@example.org", account.Email)
            Assert.Equal(SignInProvider.Password, account.Provider)
            Assert.Equal(HttpStatusCode.OK, current.StatusCode)
        }

    [<Theory>]
    [<InlineData("not-an-email")>]
    [<InlineData("missing@domain")>]
    [<InlineData("@example.org")>]
    [<InlineData("two words@example.org")>]
    [<InlineData("")>]
    member _.``username must be an email``(email: string) =
        task {
            let! http, _ = start ()
            let! response = signUp http email password

            let! error =
                expectError response HttpStatusCode.BadRequest ErrorCode.ValidationFailed

            Assert.Equal("email", error.Fields.Head.Field)
        }

    [<Fact>]
    member _.``short password is rejected``() =
        task {
            let! http, _ = start ()
            let! response = signUp http "asha@example.org" "short"

            let! error =
                expectError response HttpStatusCode.BadRequest ErrorCode.ValidationFailed

            Assert.Equal("password", error.Fields.Head.Field)
        }

    [<Fact>]
    member _.``enormous password is rejected``() =
        task {
            let! http, _ = start ()
            let! response = signUp http "asha@example.org" (String('a', 129))
            let! _ = expectError response HttpStatusCode.BadRequest ErrorCode.ValidationFailed
            return ()
        }

    [<Fact>]
    member _.``duplicate email is a conflict, whatever its case``() =
        task {
            let! http, _ = start ()
            let! _ = signUp http "asha@example.org" password
            let! response = signUp http "ASHA@Example.org" password
            let! _ = expectError response HttpStatusCode.Conflict ErrorCode.Conflict
            return ()
        }

    [<Fact>]
    member _.``password is stored hashed``() =
        task {
            let! http, db = start ()
            let! _ = signUp http "asha@example.org" password
            let! stored = scalar db "select password_hash from users"
            Assert.DoesNotContain(password, string stored)
            Assert.True((string stored).Length > 40)
        }

    [<Fact>]
    member _.``sign in works``() =
        task {
            let! http, _ = start ()
            let! _ = signUp http "asha@example.org" password
            let! response = signIn http "Asha@example.org" password
            let! current = me http response
            Assert.Equal(HttpStatusCode.OK, current.StatusCode)
        }

    [<Fact>]
    member _.``wrong password is rejected``() =
        task {
            let! http, _ = start ()
            let! _ = signUp http "asha@example.org" password
            let! response = signIn http "asha@example.org" "wrong password here"
            let! _ = expectError response HttpStatusCode.Unauthorized ErrorCode.Unauthorized
            return ()
        }

    [<Fact>]
    member _.``sign in error does not reveal accounts``() =
        task {
            let! http, _ = start ()
            let! _ = signUp http "asha@example.org" password
            let! wrong = signIn http "asha@example.org" "wrong password here"
            let! unknown = signIn http "nobody@example.org" "wrong password here"
            let! first = expectError wrong HttpStatusCode.Unauthorized ErrorCode.Unauthorized
            let! second = expectError unknown HttpStatusCode.Unauthorized ErrorCode.Unauthorized
            Assert.Equal(first.Message, second.Message)
        }

    [<Fact>]
    member _.``google account cannot sign in with a password``() =
        task {
            let! http, db = start ()

            do! sql db "insert into users (email, provider) values ('asha@example.org', 'google')"

            let! response = signIn http "asha@example.org" password
            let! _ = expectError response HttpStatusCode.Unauthorized ErrorCode.Unauthorized
            return ()
        }

    [<Fact>]
    member _.``me without a session is unauthorized``() =
        task {
            let! http, _ = start ()
            let! response = http.GetAsync Routes.me.Template
            let! _ = expectError response HttpStatusCode.Unauthorized ErrorCode.Unauthorized
            return ()
        }

    [<Fact>]
    member _.``session cookie is http only``() =
        task {
            let! http, _ = start ()
            let! response = signUp http "asha@example.org" password
            let cookie = response.Headers.GetValues "Set-Cookie" |> Seq.head
            Assert.Contains("httponly", cookie.ToLowerInvariant())
            Assert.Contains("samesite=lax", cookie.ToLowerInvariant())
        }

    [<Fact>]
    member _.``sign out ends session``() =
        task {
            let! http, _ = start ()
            let! response = signUp http "asha@example.org" password
            let! out = http.PostAsync(Routes.signOut.Template, null)
            let cookie = out.Headers.GetValues "Set-Cookie" |> Seq.head
            Assert.Equal(HttpStatusCode.OK, out.StatusCode)
            Assert.Contains("expires=thu, 01 jan 1970", cookie.ToLowerInvariant())
            Assert.NotNull response
        }

    [<Fact>]
    member _.``sign in is rate limited``() =
        task {
            let! http, _ = start ()

            for _ in 1 .. Limits.signInsPerMinute do
                let! _ = signIn http "asha@example.org" "wrong password here"
                ()

            let! blocked = signIn http "asha@example.org" "wrong password here"
            let! _ = expectError blocked HttpStatusCode.TooManyRequests ErrorCode.RateLimited
            return ()
        }

    [<Fact>]
    member _.``google sign in creates an account``() =
        task {
            let! db = scope.NewDatabase()
            migrated db
            use source = NpgsqlDataSource.Create db
            let! user = Users.claimForGoogle source "asha@example.org"
            Assert.Equal("google", user.Provider)
        }

    [<Fact>]
    member _.``google sign in removes a password someone else may have set``() =
        task {
            let! http, db = start ()
            let! _ = signUp http "asha@example.org" password
            use source = NpgsqlDataSource.Create db
            let! user = Users.claimForGoogle source "ASHA@example.org"
            let! attacker = signIn http "asha@example.org" password
            Assert.True user.PasswordHash.IsNone
            let! _ = expectError attacker HttpStatusCode.Unauthorized ErrorCode.Unauthorized
            return ()
        }

    [<Fact>]
    member _.``google sign in is off until configured``() =
        task {
            let! http, _ = start ()
            let! response = http.GetAsync Routes.googleSignIn.Template
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)
        }

    [<Fact>]
    member _.``sign-in options say whether google is offered``() =
        task {
            let! http, _ =
                TestApp.start scope Sample.catalog (fun app data -> AuthApi.mapOptions app data.Source true)

            let! options =
                http.GetFromJsonAsync<SignInOptions>(Routes.signInOptions.Template, Json.options)

            Assert.True options.Google
        }

    interface IDisposable with
        member _.Dispose() = (scope :> IDisposable).Dispose()

    [<Fact>]
    member _.``a native app signs in with a bearer token``() =
        task {
            let! http, _ = start ()
            let! _ = signUp http "app@example.org" password
            let! token = tokenFor http "app@example.org"
            let! status = withBearer http token
            Assert.Equal(HttpStatusCode.OK, status)
        }

    [<Fact>]
    member _.``a token that has been changed is refused``() =
        task {
            let! http, _ = start ()
            let! _ = signUp http "tampered@example.org" password
            let! token = tokenFor http "tampered@example.org"
            let! status = withBearer http (token.Substring(0, token.Length - 4) + "AAAA")
            Assert.Equal(HttpStatusCode.Unauthorized, status)
        }

    [<Fact>]
    member _.``a wrong password gets no token``() =
        task {
            let! http, _ = start ()
            let! _ = signUp http "wrong@example.org" password

            let! response =
                send http Routes.issueToken.Template (credentials "wrong@example.org" "not the password")

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode)
        }

    [<Fact>]
    member _.``the website may call the api with its sign-in``() =
        task {
            let! http, _ = start ()
            use request = new HttpRequestMessage(HttpMethod.Get, Routes.me.Template)
            request.Headers.Add("Origin", TestApp.testSite.Url)
            let! response = http.SendAsync request

            Assert.Equal(
                TestApp.testSite.Url,
                String.Join("", response.Headers.GetValues "Access-Control-Allow-Origin")
            )

            Assert.Equal("true", String.Join("", response.Headers.GetValues "Access-Control-Allow-Credentials"))
        }

    [<Fact>]
    member _.``another website may not``() =
        task {
            let! http, _ = start ()
            use request = new HttpRequestMessage(HttpMethod.Get, Routes.me.Template)
            request.Headers.Add("Origin", "https://evil.test")
            let! response = http.SendAsync request
            Assert.False(response.Headers.Contains "Access-Control-Allow-Origin")
        }

    interface IClassFixture<PostgresFixture>
