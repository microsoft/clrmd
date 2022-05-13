#Migrating to 2.1

Here is a summary of breaking changes in ClrMD 2.1:

## IBinaryLocator -> IFileLocator

We have removed `IBinaryLocator` and replaced it with `IFileLocator`.  Similarly the properties on `DataTarget` and `CustomDataTarget` have changed accordingly.

If you originally implemented the `IBinaryLocator` interface, you will now need to implement `IFileLocator` instead.  This interface is meant to fully capture the SSQP conventions for making symbol server requests.  The specs for which can be found at [SSQP Key Conventions](https://github.com/dotnet/symstore/blob/main/docs/specs/SSQP_Key_Conventions.md) and [SSQP CLR Private Key conventions](https://github.com/dotnet/symstore/blob/main/docs/specs/SSQP_CLR_Private_Key_Conventions.md).

Most folks don't need to implement these interface specifically, and ClrMD provides a reasonable default implementation.  Unfortunately, the best way to see how these are implemented is to look at ClrMD's implementation if you need to change behavior.

### Why did we make this change?

`IBinaryLocator` did not provide the proper interface surface to fully capture Elf and Mach-O symbol server requests and the underlying interface could not be made to work without resorting to some really bad hacks.

It was a lot cleaner to replace `IBinaryLocator` than to try to hack around the original implementation.

