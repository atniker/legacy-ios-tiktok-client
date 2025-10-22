using System;

namespace Plugin.SecureStorage.Abstractions
{
	public abstract class SecureStorageImplementationBase : ISecureStorage
	{
		public SecureStorageImplementationBase()
		{
		}

		public virtual string GetValue(string key, string defaultValue = null)
		{
			if (string.IsNullOrWhiteSpace(key))
			{
				throw new ArgumentException("Invalid parameter: key");
			}
			return null;
		}

		public virtual bool SetValue(string key, string value)
		{
			if (string.IsNullOrWhiteSpace(key))
			{
				throw new ArgumentException("Invalid parameter: key");
			}
			if (value == null)
			{
				throw new ArgumentNullException("value cannot be null.");
			}
			return false;
		}

		public virtual bool DeleteKey(string key)
		{
			if (string.IsNullOrWhiteSpace(key))
			{
				throw new ArgumentException("Invalid parameter: key");
			}
			return false;
		}

		public virtual bool HasKey(string key)
		{
			if (string.IsNullOrWhiteSpace(key))
			{
				throw new ArgumentException("Invalid parameter: key");
			}
			return false;
		}
	}
}
