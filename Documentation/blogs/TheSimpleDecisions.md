# The simple decisions

Every day are making simple decisions, they dont take much brain power from us but they take effort. You need open that email, go to that system, login, lookup it up, say, 'that is ok'

This decisions are not all the critical to us, although they might be to others, are filled in our lifes.

We can take many of these decisions and move them now to a computer, since computer understands us, it can do the lookup and make those simple decisions

And this is where Plang shines.

Now let's imagine your are customer support, you get emails in and these are the decision that need to be made

I have a client, their customer support tickets coming in is 90% the same 10 issues. What I do is create a goal for each of those issues.

One issue that is common, is the user has lost his coupon that he bought from the website.

The email body is something like this

    from: user@example.org
    Hi, I lost my coupon for the Christmas show, can you help me.

Let's solve this in plang

## The plan

The system gets the email sent to it, at that point we need to make decision

- Can we process the email
- If we can process, 
    - call the goal that matches email
- If we cannot process, dont do anything

## Code

We have three variables available to us that is sent into the plang app:
- %email% of user
- %body% is the email body
- %subject% is the email subject


Let's create the goal `ProcessEmail.goal`

```plang
ProcessEmail
- [llm] system: You are processing email.
            You should select the goal that matches the email. 
            If no goal is found, set goal as null.
            == Goals ==
            GoalName: LostCoupon
            Description: When user is asking about his lost coupons.

            ....
            == Goals ==
        user: "Subject:%subject%
                Body: %body%"
        scheme: {GoalName:string}
- if %GoalName% is not null, call goal %GoalName%
```

Next we create the file `LostCoupon.goal`. 

Next we need to query the database for this user in our database, we do that by finding the user in the database by his email `user@example.org` and all the products that he has bought.


```plang
LostCoupon
- select u.name as userName, p.name as productName, o.coupon_number 
        from users u
        join orders o on u.id=o.user_id
        join products p on p.id=o.product_id
        where u.email=%email%
    write to %orders%
- [llm] system: You are provided a list of orders for a user
    see if you can find the product and coupon he is asking for. 
    If you find the coupon that matches the product user is looking for, create email to the user, with the user name.
    == orders ==
    %orders%
    == orders ==
    user: %body%
    scheme: {isFound:bool, body:string}
- if %isFound% then call CouponFoundEmail, else call NotFoundCouponEmail

CouponFoundEmail
- send email to %email%, %body%

NotFoundCouponEmail
- send email to %email%, body="We could not find your coupon"
```
> Note: This has been simplified for this article, you need to do bit more description for the LLM and we should mark the email as process in some way, depending on your email system. 

We define the `scheme` as `{isFound:bool, body:string}`. The LLM will then give us this information that we can use.

## No database access

Let's say you don't have database access, but you have access to an admin system that can answer this question. This can be solve we the WebCrawler module

```plang
CrawlForCoupon
- open https://example.org/admin
- set #username = %Settings.Username%
- set #password = %Settings.Password




