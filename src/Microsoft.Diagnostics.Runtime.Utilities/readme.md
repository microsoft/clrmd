# Microsoft.Diagnostics.Runtime.Utilities

## What is this library?

Microsoft.Diagnostics.Runtime.Utilities is a collection of helpers that are very useful to have implemented in a common place, but do not neatly fit into the ClrMD library.
It is published here http://nuget.org/packages/Microsoft.Diagnostics.Runtime.Utilities.

## Microsoft.Diagnostics.Runtime.DbgEng

This namespace and directory is a set of helpers which help with implementing DbgEng plugins or applications which drive DbgEng using IDebug* interfaces.

### Can I contribute a wrapper here for my project?

Yes.  If I haven't wrapped a particular method you need, you are more than welcome to submit a pull request to add a wrapper for DbgEng.  I have a few requests for this before you do so:

1.  Please keep the IDebug* API as close to the original as possible.  It's totally fine to make it more C#-like, but try not to invent some totally new convention that doesn't fit with what the API does.
2.  Please keep "helper" classes to a minimum.  I'm trying to avoid the original pitfall I had with this library of just dumping a bunch of random stuff into Utilities.  Feel free to file an issue and @leculver to ask ahead of doing major work in this space to get my opinion ahead of time.

### How often will this library ship?

I will likely ship this library on a monthly-ish cadence when folks contribute PRs with newly wrapped functions.  I can't commit to faster than monthly cadence due to other projects I work on for Microsoft.  Please @leculver if I didn't see a pull request, issue, or need to ship an update.

### Why isn't this function implemented?

I have not wrapped every function in the IDebug* interfaces.  I do not intend to fully wrap all IDebug* interfaces myself, I'm only implementing these as I need them.  As stated above, you are more than welcome to implement any IDebug* interface or method yourself and submit it as a PR, but I unfortunately do not have the time to do everything myself.
