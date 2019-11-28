﻿using Lime.Protocol.Security;
using Newtonsoft.Json;
using System;
using System.Reflection;

namespace Lime.Protocol.Serialization.Newtonsoft.Converters
{
    public class AuthenticationJsonConverter : JsonConverter
    {
        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType)
        {
            return typeof (Authentication).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // The serialization is handled by the container class
            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}