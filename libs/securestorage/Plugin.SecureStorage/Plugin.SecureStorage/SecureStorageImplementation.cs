using MonoTouch.Foundation;
using Plugin.SecureStorage.Abstractions;
using MonoTouch.Security;

namespace Plugin.SecureStorage
{
	public class SecureStorageImplementation : SecureStorageImplementationBase
	{
		public override string GetValue(string key, string defaultValue)
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			base.GetValue(key, defaultValue);
			SecStatusCode ssc;
			SecRecord record = GetRecord(key, out ssc);
			if ((int)ssc == 0)
			{
				return ((object)record.ValueData).ToString();
			}
			return defaultValue;
		}

		public override bool SetValue(string key, string value)
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Invalid comparison between Unknown and I4
			base.SetValue(key, value);
			RemoveRecord(key);
			return (int)AddRecord(key, value) == 0;
		}

		public override bool DeleteKey(string key)
		{
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Invalid comparison between Unknown and I4
			base.DeleteKey(key);
			return (int)RemoveRecord(key) == 0;
		}

		public override bool HasKey(string key)
		{
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Invalid comparison between Unknown and I4
			base.HasKey(key);
			SecStatusCode ssc;
			GetRecord(key, out ssc);
			return (int)ssc == 0;
		}

        private SecStatusCode AddRecord(string key, string val)
        {
            string accessGroup = "com.atnik.tiktok"; 

            return SecKeyChain.Add(new SecRecord((SecKind)1)
            {
                Account = key,
                ValueData = NSData.FromString(val),
                AccessGroup = accessGroup
            });
        }

        private SecRecord GetRecord(string key, out SecStatusCode ssc)
        {
            string accessGroup = "com.atnik.tiktok"; 
            return SecKeyChain.QueryAsRecord(new SecRecord((SecKind)1)
            {
                Account = key,
                AccessGroup = accessGroup 
            }, out ssc);
        }

        private SecStatusCode RemoveRecord(string key)
        {
            string accessGroup = "com.atnik.tiktok"; 
            SecStatusCode ssc;
            SecRecord record = GetRecord(key, out ssc);
            if ((int)ssc == 0)
            {
                return SecKeyChain.Remove(new SecRecord((SecKind)1)
                {
                    Account = key,
                    ValueData = record.ValueData,
                    AccessGroup = accessGroup 
                });
            }
            return (SecStatusCode)(-25294); 
        }
	}
}
