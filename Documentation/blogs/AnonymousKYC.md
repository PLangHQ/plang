# In Theory: Anonymous KYC

> __In theory articles, I describe a possible implementation of a service that can be done in Plang. The code below is not production-ready but should provide a good starting point.__

## Importance of Identity

Plang has built-in [identity](../Identity.md), which means you don't need to log in to services. For each request, your identity is sent.

That identity is unique for each app.

## What We Will Do

In this example, we are going to build two apps and two web services.

### RentAHouse App

The application is located on the user's computer. The user can rent out a house on a specific date. We will only implement the renting of the house, specifically house number 123.

### KYC App

Handles the registration for KYC and is located on the user's computer. It needs information from the RentAHouse web service to be able to register.

### RentAHouse Web Service

The web service handles the centralized database of houses available for rent and books the rented period that the user requests.

The service needs to:
- Register the RentAHouse with a KYC service.
- Register the user with RentAHouse.
- Link KYC service with the user.
- Rent out the house.
- Get user personal information.

### KYC Web Service 

The KYC web service handles all the KYC information about the user. It provides a web interface to register the user, registers the user with the RentAHouse service, and returns personally identifiable information.

The service needs to:
- Register the company.
- Register the user.
- Register the user with a web service.
- Return personally identifiable information.

# Let's Start the Coding

Below is the Plang code needed to implement everything. We start with the two apps, then the two web services.

## RentAHouse

It's a simple app used for renting a house. It has one file, `Start.goal`, and one app, called KYC. This is the file & folder structure:

RentAHouse
- Start.goal
- .db
- .build
- apps 
    - KYC
        - RegisterIdentity.goal
        - .db
        - .build

My RentAHouse app has its own identity and the KYC app has its own identity. Each identity is stored in `.db/system.sqlite` of each app.

### Knowing Personal Information About the User

RentAHouse doesn't really need to know who you are when you are renting. 

The only time they would ever need to know who you are is if you trash the house and some legal action is required.

### Implement the Code

So let's implement Anonymous KYC for the RentAHouse app.

The user is going to rent a house with the id 123, January 1st to 3rd.

```plang
Start 
- Post http://rentahouse.com/api/Rent
    Data:  
        houseId=123
        startDate=2024-01-01 
        endDate=2024-01-03
    write to %response%
- If %response.status% = "KYC"
    - Call goal app/KYC/RegisterIdentity 
        serviceIdentity=%response.serviceIdentity%
        userIdentity=%response.userIdentity%
    - Call goal Start
- If %response.status% = ok
    - write out "house rented"
```

If a user has not registered their KYC with the rentahouse.com service, the service will instruct the code to register the user with the KYC service.

It calls the KYC app to do this. After the KYC registration, we call `Start` again to rent the house.

For simplification, if the user is registered we assume that the house is rented if status is "ok" and don't handle other cases. This is just an example after all.

## KYC App - RegisterIdentity

The KYC app in the apps subdirectory of the RentAHouse app would be like this:

```plang
RegisterIdentity
- Post https://KYC.com/api/RegisterUser
    Data: 
        serviceIdentity=%serviceIdentity%
        userIdentity=%userIdentity%
    write to %response%
- If %response.type% = "register"
    - Open browser %response.url%
```

## Services

We have two services that need implementation.

The interesting part is that all these services already exist in the Web 2.0 space; they already have the APIs and the code implemented. They only need to wrap Plang around it to support Identity.

## RentAHouse Service

RentAHouse is an imaginary web service, but think booking.com or airbnb.com. It has the same principles.

First, the rentahome.com would have to register as a company at the KYC service. KYC services already have this setup as web forms, or it could be done with Plang.

```plang
RegisterCompanyWithKYC
- post http://kyc.com/api/RegisterCompany
    Data: name="rentahome.com"
        contact=info@rentahome.com
        / and some other info
    write %response%
- write out %response% 
```
After running the code, rentahome.com is registered with the KYC service. 

Lets create the renting code. 

Create a folder `api`, in there we create `Rent.goal`.

```plang
Rent
- select user_id, is_kyc_approved from users where %Identity%, return 1 row
- if %user_id% is empty
    - insert into users, %Identity%, write to %user_id%
- if %is_kyc_approved% is empty or false
    - GET https://kyc.com/api/GetIdentity?userIdentity=%Identity%, write to %identityResponse%
    - if %identityResponse.isRegisterd% is false then
        - write out error, '{
            status:"KYC", 
            userIdentity:%Identity%, 
            serviceIdentity:%identityResponse.serviceIdentity%
            }'
    - if %identityResponse.isRegisterd% is true then
        - update user, set is_kyc_approved=true where user_id=%user_id%
- post https://internal-api.rentahouse.com/rent
    Data:
        houseId=%request.house_id%
        startDate=%request.startDate%
        endDate=%request.endDate%
        userId=%user_id%
    write to %response%
- write out %response%
```

### Explaining This Code

We start by finding the user in the database by their Identity. For this example, we create the user in the database if they don't exist.

```plang
- select user_id, is_kyc_approved from users where %Identity%, return 1 row
- if %user_id% is empty
    - insert into users, %Identity%, write to %user_id%
```

As you can see, no personal information is stored here, only the `%Identity%`. Notice "return 1 row": when you specify that it's only going to return 1 row, Plang loads the columns straight into variables, such as `%user_id%` and `%is_kyc_approved%`.

Next, we check if the KYC has been registered. If not, then we need to check if the user has registered with the KYC service without us knowing who they are, and if they haven't, ask them to register.

```plang
- if %is_kyc_approved% is empty or false
    - GET https://kyc.com/api/GetIdentity?userIdentity=%Identity%, write to %identityResponse%
    - if %identityResponse.isRegisterd% is false then
        - write out error, '{
            status:"KYC", 
            userIdentity:%Identity%, 
            serviceIdentity:%identityResponse.serviceIdentity%
            }'
    - if %identityResponse.isRegisterd% is true then
        - update user, set is_kyc_approved=true where user_id=%user_id%
```

If the user hasn't registered, they will get the necessary info to send to the KYC app to register.

If the user is registered, the user table is updated, and the next step is to rent out the house by posting to an already existing API that the service has.

## KYC.com Service

The KYC.com services already exist out there. Companies use them to get KYC. This is all about anonymous KYC, so the companies wouldn't store any personal data but get the data when needed (if the user trashes the home they rented).

### Table Structure

We assume that the KYC service has the following table structure in the database:
- users - contains Identity column and personally identifiable user information, such as name, email, etc.
- companies - contains Identity column and company information, such as name, address, phone
- users_companies - links users and companies together

### Code Implementation

Let's start a web server:

```plang
Start
- start webserver
```

Let's start by implementing the company registration.

Create a file in the `api` folder, `RegisterCompany.goal`:

```plang
RegisterCompany
- select id as companyId from companies where %Identity%, return 1 row
- if %companyId% is empty
    - insert into company %Identity%, 
            %request.name%, %request.address%, %request.phone%
        write to %companyId%
- write out 'Company registered'
```

This goal is called by the rentahouse.com server, specifically the `RegisterCompanyWithKYC` we have above.

The KYC.com service now has the `%Identity%` of the rentahouse.com website. This allows the rentahouse.com service to use KYC.com.

The KYC service also needs to connect the user with the identity of the RentAHouse app because it is not the same as the KYC app on their computer.

The KYC service would then implement a service that does this.

Create `RegisterUser.goal` in the `api` folder:

```plang
RegisterUser
- select id from users where %Identity%
- if %id% is empty then
    - write out {type:"register", url:"https://kyc.com/register?identity=%Identity%"}
- if %id% is not empty then
    - select name, address, phone from users u
        join users_companies uc on uc.user_id=u.id
        where uc.serviceIdentity=%Identity% and uc.identity=%userIdentity%
        write to %userInfo%
    - if %userInfo% is empty
        - insert into users_companies %Identity%, %serviceIdentity%
- write out 'ok'
```

If the user has registered with kyc.com he will get the url, open it in browser and fill out the registration web form. KYC services already have this web forms setup. The point is, we don't need to reinvent things that already exist. 

After the user has registered, we link the user with the company

This goal is called by the KYC app on the user's computer, `RegisterIdentity`.

Next, we need to create the `GetIdentity` API. It would look like this:

Create `GetIdentity.goal` in the `api` folder:

```plang
GetIdentity
- select id from users_companies 
        where userIdentity=%request.userIdentity% and 
        serviceIdentity=%request.serviceIdentity%, 
    write to %id%
- if %id% is empty then
    - write out error '{isRegistered:false, serviceIdentity:%Identity%}
- write out {isRegistered:true}
```

It checks if this user and company are linked. If they are not, then the `serviceIdentity` is provided, and `isRegistered` is false.

The RentAHouse app on the user's computer then calls the KYC app, providing the identity of the user and service.

If the user then trashes the house, the rentahouse.com service can request information about the user. Depending on the KYC service agreement, this might involve a court order or some other form of request.

The `api/GetUserInfo` would then look like this:

```plang
GetUserInfo
- select name, address, phone from users u
    join users_companies uc on uc.user_id=u.id
    where uc.serviceIdentity=%Identity% and uc.identity=%userIdentity%
    write to %userInfo%
- get ip address, write to %ip%
- insert into user_request, 
    serviceIdentity=%Identity%, 
    userIdentity=%userIdentity%, ip=%ip%
- write out %userInfo%
```

In this case we are just writing down the request into user_request table, but of course the kyc service can implement their own version of this.

## Is This Anonymous KYC?

Well, KYC is in its essence not anonymous. The job is to know who you are. But this allows the rentahouse.com service to be completely unaware of who you are until it really needs to know.

This means that the rentahouse.com service is not storing any personal information. Personal information is the main incentive to attack a service, to extract it and sell it.

## What Next

As I said at the start of the article, this code is not full code. You will be able to compile it and run it, but you need to handle edge cases and errors.

It's about 90% there, so only the [last 90% is left](https://en.wikipedia.org/wiki/Ninety%E2%80%93ninety_rule) to code.

The good news is that it took me about 1 hour to write this code, so if the rule applies, you have a few hours' work to have fully production-ready code.