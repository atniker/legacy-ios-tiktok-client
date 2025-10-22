using System;
using System.Threading;
using Plugin.SecureStorage.Abstractions;

namespace Plugin.SecureStorage
{
	public class CrossSecureStorage
	{
		private static Lazy<ISecureStorage> Implementation = new Lazy<ISecureStorage>((Func<ISecureStorage>)(() => CreateSecureStorage()), (LazyThreadSafetyMode)1);

		public static ISecureStorage Current
		{
			get
			{
				ISecureStorage value = Implementation.Value;
				if (value == null)
				{
					throw NotImplementedInReferenceAssembly();
				}
				return value;
			}
		}

		private static ISecureStorage CreateSecureStorage()
		{
			return new SecureStorageImplementation();
		}

		internal static Exception NotImplementedInReferenceAssembly()
		{
			return new NotImplementedException("This functionality is not implemented in the portable version of this assembly.  You should reference the NuGet package from your main application project in order to reference the platform-specific implementation.");
		}
	}
}
