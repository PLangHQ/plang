# HttpModule

Hint: `[http]`  
Type: `PLang.Modules.HttpModule.Program`

Make Http request. Mare sure to format Bearer authentication correctly in headers variable

## Methods

### Delete

```
Delete(String url, Object data = null, Boolean doNotSignRequest = False, Dictionary<String, Object> headers = null, String encoding = utf-8, String contentType = application/json, Int32 timeoutInSeconds = 30) : Object
```


### DownloadFile

```
DownloadFile(String url, String pathToSaveTo, Boolean overwriteFile = False, Dictionary<String, Object> headers = null, Boolean createPathToSaveTo = True, Boolean doNotDownloadIfFileExists = False) : String
```


### Get

```
Get(String url, Object data = null, Boolean doNotSignRequest = False, Dictionary<String, Object> headers = null, String encoding = utf-8, String contentType = application/json, Int32 timeoutInSeconds = 30) : Object
```


### Head

```
Head(String url, Object data = null, Boolean doNotSignRequest = False, Dictionary<String, Object> headers = null, String encoding = utf-8, String contentType = application/json, Int32 timeoutInSeconds = 30) : Object
```


### Option

```
Option(String url, Object data = null, Boolean doNotSignRequest = False, Dictionary<String, Object> headers = null, String encoding = utf-8, String contentType = application/json, Int32 timeoutInSeconds = 30) : Object
```


### Patch

```
Patch(String url, Object data = null, Boolean doNotSignRequest = False, Dictionary<String, Object> headers = null, String encoding = utf-8, String contentType = application/json, Int32 timeoutInSeconds = 30) : Object
```


### Post

```
Post(String url, Object data = null, Boolean doNotSignRequest = False, Dictionary<String, Object> headers = null, String encoding = utf-8, String contentType = application/json, Int32 timeoutInSeconds = 30) : Object
```


### PostMultipartFormData

```
PostMultipartFormData(String url, Object data, String httpMethod = POST, Boolean doNotSignRequest = False, Dictionary<String, Object> headers = null, String encoding = utf-8, Int32 timeoutInSeconds = 30) : Object
```

Post a FileStream to url. When a variable is defined with @ sign, it defines that it should be a FileStream. data may contain something like file=@%fileName%;type=%fileType%, then keep as one value for the file parameter. The function will parse the file and type


### Put

```
Put(String url, Object data = null, Boolean doNotSignRequest = False, Dictionary<String, Object> headers = null, String encoding = utf-8, String contentType = application/json, Int32 timeoutInSeconds = 30) : Object
```


### Request

```
Request(PLang.Modules.HttpModule.Program+HttpRequest request) : Object
```

- `request` *PLang.Modules.HttpModule.Program+HttpRequest* — (see Type information in SupportingObjects)

### Request

```
Request(String url, String method, Object data = null, Boolean doNotSignRequest = False, Dictionary<String, Object> headers = null, String encoding = utf-8, String contentType = application/json, Int32 timeoutInSeconds = 30) : Object
```


### SendBinaryOfFile

```
SendBinaryOfFile(String url, String filePath, String httpMethod = POST, Dictionary<String, Object> requestHeaders = null, Dictionary<String, Object> contentHeaders = null, String encoding = utf-8, Int32 timeoutInSeconds = 30) : Object
```

Send binary file to server. Make sure to set correct headers on correct header variable, requestHeaders or contentHeader


