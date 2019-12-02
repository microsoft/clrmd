namespace Microsoft.Diagnostics.Runtime
{
    static class Helpers
    {
        public static ClrType GetTypeByName(this ClrModule module, string name)
        {
            foreach ((ulong mt, uint _) in module.EnumerateTypeDefToMethodTableMap())
            {
                ClrType type = module.AppDomain.Runtime.GetTypeByMethodTable(mt);
                if (type.Name == name)
                    return type;
            }

            return null;
        }
    }
}
