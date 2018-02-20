[![Build status](https://ci.appveyor.com/api/projects/status/458hoqaj6dr94jrj?svg=true)](https://ci.appveyor.com/project/ppittle/httpwebrequestwrapper)
[![AppVeyor tests](https://img.shields.io/appveyor/tests/ppittle/httpwebrequestwrapper.svg?logo=appveyor)](https://ci.appveyor.com/project/ppittle/httpwebrequestwrapper/build/tests)
[![Coverage Status](https://coveralls.io/repos/github/ppittle/HttpWebRequestWrapper/badge.svg)](https://coveralls.io/github/ppittle/HttpWebRequestWrapper)
[![NuGet](https://img.shields.io/nuget/v/HttpWebRequestWrapper.svg)](https://www.nuget.org/packages/HttpWebRequestWrapper)
[![NuGet](https://img.shields.io/nuget/dt/HttpWebRequestWrapper.svg)](https://www.nuget.org/packages/HttpWebRequestWrapper)

# HttpWebRequestWrapper

**HttpWebRequestWrapper** is a testing layer for Microsoft's `HttpWebRequest` and `WebClient` classes. It overcomes restrictions that would normally prevent mocking a `HttpWebRequest` and allows testing your application with faked HTTP requests in Unit and BDD tests.

Ideal for testing application code that relies on http api calls either directly or through 3rd party libraries!

## NuGet

    PM> Install-Package HttpWebRequestWrapper

HttpWebRequestWrapper has no 3rd Party dependencies!

## Usage

Let's say you have some simple but very hard to test code that uses `WebRequest.Create` to make a live http call:

```csharp
public static class Example
{
    public static int CountCharactersOnAWebPage(string url)
    {
       // makes live network call
       var response = (HttpWebResponse)WebRequest.Create(url).GetResponse();

       if (response.StatusCode != HttpStatusCode.Ok)
          throw new Exception();

       using (var sr = new StreamReader(response.GetResponseStream())
            return sr.ReadToEnd().Length;
    }
}
```
  

Easily unit test your code with the **HttpWebRequestWrapper** Library:

```csharp
// ARRANGE
var fakeResponseBody = "<html>Test</html>";

using (new HttpWebRequestWrapperSession(
    new HttpWebRequestWrapperInterceptorCreator(req => req.HttpWebResponseCreator.Create(fakeResponseBody))))
{
    // ACT 
    var charactersOnGitHub = Example.CountCharactersOnAWebPage("http://www.github.com");

    // ASSERT
    Assert.Equal(fakeResponseBody.Length, charactersOnGitHub);
}
```

### Full Mocking

Go crazy with full mocking support!  The ` HttpWebRequestWrapperInterceptor` provides very powerful faking, but you can easily build your own mock `HttpWebRequestWrapper` to provide custom behavior or expectations.

```csharp
// ARRANGE
var fakeResponseBody = "Fake Response";
var mockWebRequest = new Mock<HttpWebRequestWrapper>(new Uri("http://www.github.com"));
mockWebRequest
    .Setup(x => x.GetResponse())
    .Returns(HttpWebResponseCreator.Create(
        new Uri("http://www.github.com"), 
        "GET",
         HttpStatusCode.OK, 
         "Fake Response"));

var mockCreator = new Mock<IWebRequestCreate>();
mockCreator
    .Setup(x => x.Create(It.IsAny<Uri>()))
    .Returns(mockWebRequest.Object);

// ACT
string responseBody;
using (new HttpWebRequestWrapperSession(mockCreator.Object))
{    
    request = (HttpWebRequest)WebRequest.Create("http://www.github.com");

    using (var sr = new StreamReader(request.GetResponse().GetResponseStream()))
        responseBody = sr.ReadToEnd();
}

// ASSERT
Assert.Equal(fakeResponseBody, responseBody);
mockWebRequest.Verify(x => x.GetResponse());

```

### WebClient Support

 **HttpWebRequestWrapper** also fully supports the .net `WebClient` class:

```csharp
var fakeResponse = "Testing";

using (new HttpWebRequestWrapperSession(
    new HttpWebRequestWrapperInterceptorCreator(
        x => x.HttpWebResponseCreator.Create(fakeResponse))))
{
    var responseBody = new WebClient().DownloadString("https://www.github.com");

    Assert.Equal(fakeResponse, responseBody);
}
```

### Advanced Record and Playback

Use the `HttpWebRequestWrapperRecorder` to capture all http requests and response into a serializable `RecordingSession` for later playback in reliable and consistent tests!

```csharp
// run the application using the recorder

var recordingSession = new RecordingSession();
using (new HttpWebRequestWrapperSession(new HttpWebRequestWrapperRecorderCreator(recordingSession)))
{
   Example.CountCharactersOnAWebPage("http://www.github.com");
}

// serialize the recordingSession to disk (and preferrably embed in test assembly)
File.WriteAllText(@"c:\recordingSession.json", JsonConvert.Serialize(recordingSession));
```
Next, deserialize the recording session json and use the `RecordingSessionInterceptorRequestBuilder`  to feed the `RecordingSession` into the `HttpWebRequestWrapperInterceptor`

```csharp
var recordingSession = JsonConvert.DeserializeObject<RecordingSession>(json);

using (new HttpWebRequestWrapperSession(new HttpWebRequestWrapperInterceptorCreator(
    new RecordingSessionInterceptorRequestBuilder(recordingSession))))
{
    // now no http calls!
    var count = Example.CountCharactersOnAWebPage("http://www.github.com");
}

Assert.AreEqual(4321, count);
```
#### Playback Kung Fu

`RecordingSessionInterceptorRequestBuilder` exposes multiple customization points to override how matching is performed, how responses are built, and what to do if a match can't be found:

```csharp
var recordingSession = JsonConvert.DeserializeObject<RecordingSession>(json);

new RecordingSessionInterceptorRequestBuilder(recordingSession)
{
   MatchingAlgorithm = (interceptedRequest, recordedRequet) =>
       // match only on url
       interceptedReq.HttpWebRequest.RequestUri == recordedRequet.Url,

   RecordedResultResponseBuilder = (recordedReq, interceptedReq) =>
       // manipulate the response body
       interceptedReq.HttpWebResponseCreator.Create(recordedRequest.Response.ToLower()),

    RequestNotFoundResponseBuilder = interceptedReq =>
       // return a 500 when page isn't found
       interceptedReq.HttpWebResponseCreator.Create("Server Error", HttpStatusCode.InternalServerError),

    OnMatch = (recordedReq, interceptedReq, httpWebResponse, exception) =>
       // keep a count of requests made or do additional manipulation of the web response
       Log.Write("Application made another request");

    AllowReplayingRecordedRequestsMultipleTimes = 
        // act like a playback script - each recorded request in the recording 
        // session will only be used one - useful for testing error handling / retry
        // behavior
        false
};
```

But if that's not enough, you can easily implement your own `IInterceptorRequestBuilder` for full control!

#### WebException Support

Any Exception thown during `HttpWebRequest.GetResponse()` is captured by `HttpWebRequestWrapperRecorder` and is rethrown by `RecordingSessionInterceptorRequestBuilder` during playback!  This includes a fully populated `WebException` with a `WebException.Response`.

Additionally, `RequestNotFoundResponseBuilder` also supports throwing exceptions, you're free to overload and throw a NotFound WebException.

#### Dynamic Playback

Want to change up playback mid test run?  Not a problem!  You can easily manipulate `RecordingSessionInterceptorRequestBuilder.RecordedRequests` at any time:

```csharp
var builder = new RecordingSessionInterceptorRequestBuilder();
builder.RecordedRequests.Add(new RecordedRequest
{
    Url = "http://www.github.com",
    Method = "GET",
    Response = "Hello World"
};

using (new HttpWebRequestWrapperSession(new HttpWebRequestWrapperInterceptorCreator(
        builder)))
{
    var count1 = Example.CountCharactersOnAWebPage("http://www.github.com");

    builder.RecordedRequests.Clear();
    builder.RecordedRequests.Add(new RecordedRequest
    {
        Url = "http://www.github.com",
        Method = "GET",
        Response = "!!!"
    });

    var count2 = Example.CountCharactersOnAWebPage("http://www.github.com");
}

Assert.AreEqual(count1, count2);

```

#### Playback Multiple Sessions

Have a lot of network requests recorded?  Split them up over multiple recording sessions!

```csharp
var recordingSession1 = JsonConvert.DeserializeObject<RecordingSession>(json1);
var recordingSession2 = JsonConvert.DeserializeObject<RecordingSession>(json2);

using (new HttpWebRequestWrapperSession(new HttpWebRequestWrapperInterceptorCreator(
    new RecordingSessionInterceptorRequestBuilder(recordingSession1, recordingSession2))))
```

### Custom HttpWebRequest Implementations

Inspired by `HttpWebRequestWrapperInterceptor` and `HttpWebRequestWrapperRecorder` and want to build your own custom http request wrapper?  Not a problem, add an `IWebRequestCreate` to go with it and you'll be good to use `HttpWebRequestWrapperSession` to take care of the plumbing for you:

```csharp
public class CustomWrapper : HttpWebRequestWrapper
{
   public CustomWrapper(Uri uri) : base Uri{}
}

public class CustomWrapperCreator : IWebRequestCreate
{
   public WebRequest Create(Uri uri)
   {
      return new HttpWebRequestWrapperInterceptor(uri);
   }
}

using(new HttpWebRequestWrapperSession(new CustomWrapperCreator()))
{
   var request = WebRequest.Create("http://www.github.com");

   Assert.IsType<CustomWrapper>(request);
}
```

### Multiple WebRequestCreate

Have an advanced scenario where you need to use multiple `IWebRequestCreate` objects?  You can use the `HttpWebRequestWrapperDelegateCreator` to decide just-in-time which `IWebRequestCreate` to use for a specific Uri:

```csharp
var creatorSelector = new Func<Uri, IWebRequestCreate>(url =>
    url.Contains("api1")
        ? api1InterceptorCreator
        : commonInterceptorCreator);

using (new HttpWebRequestWrapperSession(new HttpWebRequestWrapperDelegateCreator(creatorSelector)))
{
    // handled by api1Interceptor
    WebRequest.Create("http://3rdParty.com/api1/request");
    // handled by commonInterceptor
    WebRequest.Create("http://someother.site");
}
```

## Secret Sauce

**HttpWebRequestWrapper** works by inheriting from `HttpWebRequest`.  This doesn't seem revolutionary, except these are the `HttpWebRequest` constructors:

```csharp
[Obsolete("This API supports the .NET Framework infrastructure and is not intended to be used directly from your code.", 
    true)]
public HttpWebRequest(){}
internal HttpWebRequest(Uri uri, ServicePoint servicePoint)
{
    // bunch of initialization code
}
```

Niether constructor will allow compilation of code that tries to inherit from `HttpWebRequest`.

### ildasm

Instead `HttpWebRequestWrapper` is compiled to inherit from `WebRequest`. During build, the compiled assembly is deassembled, has it's IL manipulated so that it inherits from `HttpWebRequest` and is then reasembled.  `HttpWebRequestWrapper` also does a bunch of reflection inside its constructor so the end result is a public class with a public constructor that inherits from `HttpWebRequest` and is fully functional!

### WebRequest.PrefixList

The second piece of magic is hooking into `WebRequest.PrefixList`.  `WebRequest` works as a factory for factories.  The `PrefixList` contains the registration of which factory is matched to which protocol.  `HttpWebRequestWrapperSession` works by overriding the default `PrefixList`, replacing the `IWebRequestCreate` for `http` and `https`.  

Disposing of the Session restores the original `Prefix` list.

### Limitations

**HttpWebRequestWrapper** can *not* support concurrent test execution.  Becuase `HttpWebRequestWrapperSession` works by setting a global static variable it's not possible to have two Sessions in use at one time.  Code inside the Session's using block is free to execute concurrently, you just can't try and use two or more Sessions at once.

### HttpClient

`HttpClient` is not supported by **HttpWebRequestWrapper** because while it does use `HttpWebRequest` under the hood, it does not honor the `WebRequest.PrefixList` for determining which `IWebRequestCreate` to use to create the `HttpWebRequest` it uses.  So `HttpWebRequestWrapperSession` can't hook into it.

If you're looking for testing tools specifically for `HttpClient`, try [MockHttp](https://github.com/richardszalay/mockhttp).

## Platform Support

The actual wrapper `HttpWebRequestWrapper` is compiled for .NET Framework 2.0 and supports up to versions 4.7 of the .NET Framework (newest version tested at the time).

The remainder of **HttpWebRequestWrapper** requires .NET Framework 3.5+

## Build

Clone the repository and build `/src/HttpWebRequestWrapper.sln` using Visual Studio. NuGet package restore must be enabled.

To generate a nuget package run:

    nuget pack .\src\HttpWebRequestWrapper\HttpWebRequestWrapper.csproj

NOTE: I recommend unloading the `HttpWebRequestWrapper.LowLevel` project after opening the solution in Visual Studio and building for the first time.  The IDE (and tools like ReSharper) get confused by `HttpWebRequestWrapper` inheriting from `WebRequest` in source code and `HttpWebRequest` in the built dll.  Consequence is a lot of warnings and false errors.