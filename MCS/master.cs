using System;
namespace MCS
{
    public class master
    {
        MaxAmp2.MAS2 httpServer = null;

        public master()
        {
			httpServer.RegisterErrorHandler(Handle, "S");
			httpServer.RegisterErrorHandler(Handle, "RC");
			httpServer.RegisterErrorHandler(Handle, "HC");
			httpServer.RegisterErrorHandler(Handle, "PU");
			httpServer.RegisterErrorHandler(Handle, "HFR");
			httpServer.RegisterErrorHandler(Handle, "GD");
			httpServer.RegisterErrorHandler(Handle, "GFC");
			httpServer.RegisterErrorHandler(Handle, "PP");
			httpServer.RegisterErrorHandler(Handle, "STF");
			httpServer.RegisterErrorHandler(Handle, "RF");
			httpServer.RegisterErrorHandler(Handle, "GE");
			httpServer.RegisterErrorHandler(Handle, "GH");
			httpServer.RegisterErrorHandler(Handle, "ST");
        }

		public static int Handle(MaxAmp2.ErrorArg EA)
		{
			Console.WriteLine("Severity:" + EA.Severity);
			Console.WriteLine("Location:" + EA.Location);
			Console.WriteLine("Date:" + EA.TimeAndDate);
			Console.WriteLine("===============================");

			return 1;
		}
    }
}
