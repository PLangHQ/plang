# HttpRequest in Plang

Plang allows the developer to do http request to external services.

## Before We Start
Make sure to [install Plang](https://github.com/PLangHQ/plang/blob/main/Documentation/Install.md) if you want to build and run this code.

## GET request

Let's jump right into it. 

Create a folder on your computer, name it `HttpTests`

Inside the folder create `Get.goal`

```plang
Get
- get https://jsonplaceholder.typicode.com/posts/1
    write to %response%
- write out '
        userId: %response.userId%
        title: %response.title%
        body: %response.body%'
```

In this code, we retrieve the content of https://jsonplaceholder.typicode.com/posts/1 and write out the response that we get.

Let's build the code and run it.

```bash
plang exec Get
```

It should print out the content of the response.

```bash

```

## POST request



