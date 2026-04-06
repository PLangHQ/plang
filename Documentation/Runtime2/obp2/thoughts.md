so obp pattern, object based pattern.
I had the idea that I needed to scrap what I had been writing and rewrite it all with claude, it was slowly realizing it, but to make it work, I felt my current architecture wasn´t good. it complex for llm, even through they can keep a lot of context, the more context the worse the result, and when you have many logical things working together, that can become an issue. and thats runtime1.
so I am programming i plang, and I am thinking, I am just sending some data between lines, I dont know whats in them, it just flows.
- read file.txt, write to %data%
- store %data%
- write out %data%

in three lines I read a file, stored in db and printed it out. I have no idea what is in %data%
in my programming, in the architect of runtime1, I had to know the details of each object. it´s a lot of work, sending parameters between methods, checking if this does that.
so I was thinking, how can I accomplish this in c#, well, what if the data that is coming in is always Data, I have no idead what is in it, it´s just there. and that would mean only one person or code or place would know how it looks, which means, that object needs to be 100% responsible for it. So all logic must move to the object.
So I have Goal class, that class that is usually a DTO, or with limited of methods, maybe ToString or such, suddenly gets Load, Save
So know my code looks like this

var data = request.form["data"];
app.goal.save(data);

now the middle layer is done, two lines of code. the middle layer is often huge, with a lot of code jumping though things.
now there is this "app" thing and where do we know where to save, or you just moved the logic
we just moved the logic, yes, but look where, what does app.goal.save tell you, you are saving a goal. beautiful.
the app, so in this pattern I call OBP, there are rules. with the benefit of the pattern is because there are rules, if you follow them you get X benefit, en first one of those rules are. you must have a root object, in this case it's "app". app is the top layer of the code, and you can always access it. when we write our goal save method we say

public Save(Data data) {
    app.user.data.save(data, 'goal');
}

but do you see how easy it is, how easy it is to understand, and llm loves this, it can traverse the code like no other.
and there is another benefit, you never load any data until you need it
let say for example the class Path,

var path = new Path("/file.txt")
path.size // how big is the file

so today, we would do something like this

public class Path

private fileinfo;
new(string path) {
    fileinfo= new FileInfo(path);   <= all info is loaded, the size and all other. but we haven't really asked for that, we just wanted path. loading it all costs cpu. there is friction. we just wasted cpu on something we might never need.

in plang, that path.size, only runs when it is is needed, on using cpu it needs.

friction goes to almost zero, so it only does what is suppose to do. nothing is loaded on new path(string) in OBP. only what the path is. when it´s needed we can work with it. and calling that method, took 4 refreences in memory, mabye 10ns. can you imagine what you do today just saving user info. how many lines of code that is.
so I decided, lets start writing plang in claude, rewrite from the scratch, 3,5 years, that is around febrary 20th, week before vacation. I started, and it was flying. OBP really working, but claude really easy to mess upp.
