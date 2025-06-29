namespace ApiTestingAgent.Data
{
    public class ArgumentValidationHelper
    {
        public static class Ensure
        {
            public static T NotNull<T>(T arg, string argName)
            {
                if (arg == null)
                {
                    throw new ArgumentNullException(argName);
                }

                return arg;
            }
        }
    }
}
