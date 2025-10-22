namespace Plugin.SecureStorage.Abstractions
{
	public interface ISecureStorage
	{
		string GetValue(string key, string defaultValue = null);

		bool SetValue(string key, string value);

		bool DeleteKey(string key);

		bool HasKey(string key);
	}
}
