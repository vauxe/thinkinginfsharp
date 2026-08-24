namespace ThinkingInFSharp.ContractTests

open System
open System.Net
open System.Net.Http
open System.Text
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open ThinkingInFSharp.Ecosystem.Web
open Xunit

module WebSampleTests =
    let private complete (operation: Task<'value>) = operation.GetAwaiter().GetResult()
    let private completeUnit (operation: Task) = operation.GetAwaiter().GetResult()

    type private TestApi() =
        let builder = WebApplication.CreateBuilder([||])

        do builder.WebHost.UseTestServer() |> ignore

        let application = builder.Build()

        do
            WebSample.map application
            application.StartAsync() |> completeUnit

        let client = application.GetTestClient()

        member _.Client = client

        interface IDisposable with
            member _.Dispose() =
                client.Dispose()
                application.DisposeAsync().AsTask() |> completeUnit

    let private post (client: HttpClient) (contentType: string) (body: string) =
        use content = new StringContent(body, Encoding.UTF8, contentType)
        client.PostAsync("/api/greetings", content) |> complete

    let private readText (response: HttpResponseMessage) =
        response.Content.ReadAsStringAsync() |> complete

    [<Fact>]
    let ``minimal api trims valid input and returns one public json shape`` () =
        use api = new TestApi()
        use response = post api.Client "application/json" """{"name":"  Ada  "}"""

        Assert.Equal(HttpStatusCode.OK, response.StatusCode)
        Assert.Equal("application/json; charset=utf-8", string response.Content.Headers.ContentType)
        Assert.Equal("""{"message":"Hello, Ada!"}""", readText response)

    [<Theory>]
    [<InlineData("application/json", "{not-json", 400, "invalid_json")>]
    [<InlineData("application/json", "{}", 400, "name_required")>]
    [<InlineData("application/json", "{\"name\":\" \"}", 400, "name_required")>]
    [<InlineData("application/json", "{\"Name\":\"Ada\"}", 400, "invalid_json")>]
    [<InlineData("application/json", "{\"name\":\"Ada\",\"extra\":true}", 400, "invalid_json")>]
    [<InlineData("text/plain", "{\"name\":\"Ada\"}", 415, "unsupported_media_type")>]
    let ``minimal api rejects transport and validation failures with stable safe codes``
        contentType
        body
        expectedStatus
        expectedCode
        =
        use api = new TestApi()
        use response = post api.Client contentType body
        let responseText = readText response

        Assert.Equal(enum<HttpStatusCode> expectedStatus, response.StatusCode)
        Assert.Contains($"\"code\":\"{expectedCode}\"", responseText)
        Assert.DoesNotContain(body, responseText)
