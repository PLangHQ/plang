# Roadmap for Plang

Here's a rough estimate of Plang's current roadmap.

## v0.1.X.X – The Buggy One

This is where we’re at with Plang right now. Plenty of modules and ideas have been implemented, but they're not thoroughly tested. Some might not work at all, others might partially function, or behave unpredictably. The core code might contain critical bugs between versions (I’m doing my best to avoid that). Right now, unit test coverage is not great, and the code structure might undergo significant changes between versions, which could mean you'll need to rebuild your Plang project along the way.

I ([@ingig](https://x.com/ingig)) am still figuring out Plang's full potential as I build it. Even though I'm the creator, I don't fully grasp everything it can become just yet. My process has always been to hack together a project first, then stabilize it once I better understand how it all fits.

There’s a lot still to be done—check out the issue tracker for specifics. I haven’t set strict boundaries for what should go into this version, but it needs to be solid enough. The one thing that *must* be done before moving on to the next version is getting the GUI working across all platforms: Windows, Linux, macOS, Android, and iOS.

## v0.2.X.X – Stability

At this stage, the changes to modules will become fewer and eventually stop altogether.
- Refactoring the code
- Writing unit tests for everything
- Implementing automated testing
- Automated code correction
- Ensuring backward compatibility indefinitely

## v0.3.X.X – Efficiency

This version is all about making Plang more efficient. Right now, the language relies heavily on reflection—how can we optimize that? What improvements can be made to the memory stack? These are the questions we’ll tackle in v0.3.

## v0.4.X.X – Go

By this point, Plang should be ready for production. This might become v1. I have a theory that once we hit v1, the core of the language won’t need to change anymore, with only security updates being necessary. But, of course, this is just a theory—Plang’s potential is still largely unexplored, and who knows what we’ll discover along the way?

## Timeline

This is the rough idea I’m working with. If you want to implement something from v0.2 while we're still in v0.1, go for it. 

I expect v0.1 to take the longest, with each subsequent version requiring less time. We published v0.1 in March 2024, and my goal is to reach v0.4 by March 2026.