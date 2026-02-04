using System.Collections.Generic;
using System.Xml;


namespace GigE_Cam_Simulator
{
    public class PropertyItem
    {
        public string RegisterName { get; }
        public eBootstrapRegister Register => BootstrapRegisterHelper.RegisterEnumByName(this.RegisterName);
        public uint RegisterAddress { get; }

        public string? StringValue { get; set; }
        public bool IsString { get; set; }

        public int IntValue { get; set; }
        public bool IsInt { get; set; }

        public uint[]? Bits { get; set; }
        public bool IsBits { get; set; }


        public PropertyItem(string registerNameOrAddress)
        {
            this.RegisterName = registerNameOrAddress;

            if (registerNameOrAddress.StartsWith("0x"))
                this.RegisterAddress = uint.Parse(registerNameOrAddress[2..], System.Globalization.NumberStyles.HexNumber);
            else
                this.RegisterAddress = BootstrapRegisterHelper.RegisterAddressByName(registerNameOrAddress);
        }
    }

    class RegisterConfig
    {
        public List<PropertyItem> Properties { get; }

        public RegisterConfig(string fileName) 
        {
            this.Properties = ReadConfigFile(fileName);
        }

        private static List<PropertyItem> ReadConfigFile(string filePath)
        {
            var properties = new List<PropertyItem>();

            var xmlDoc = new XmlDocument();
            xmlDoc.Load(filePath);

            var propertyNodes = xmlDoc.SelectNodes("//property");
            if (propertyNodes == null) //So weird. XmlNodeList doesn't have an empty version.
                return properties;     //Resort to early exit instead. Ref: https://timbar.blogspot.com/2010/10/empty-xmlnodelist-and-avoiding-null.html

            foreach (XmlNode propertyNode in propertyNodes)
            {
                XmlNode? registerNode = propertyNode.SelectSingleNode("register");
                if (registerNode == null)
                {
                    continue;
                }

                var property = new PropertyItem(registerNode.InnerText);

                // read string values
                var stringNode = propertyNode.SelectSingleNode("string");
                if (stringNode != null)
                {
                    property.StringValue = stringNode.InnerText;
                    property.IsString = true;
                }

                // read bit values
                var bitNodes = propertyNode.SelectNodes("bit");
                if (bitNodes != null && bitNodes.Count > 0)
                {
                    uint[] bits = new uint[bitNodes.Count];
                    for (int i = 0; i < bitNodes.Count; i++)
                    {
                        bits[i] = uint.Parse(bitNodes[i]!.InnerText);
                    }

                    property.Bits = bits;
                    property.IsBits = true;
                }

                // read bit values
                var intNodes = propertyNode.SelectSingleNode("int");
                if (intNodes != null )
                {
                    if (intNodes.InnerText.StartsWith("0x"))
                        property.IntValue = int.Parse(intNodes.InnerText[2..], System.Globalization.NumberStyles.HexNumber);
                    else
                        property.IntValue = int.Parse(intNodes.InnerText);
                    property.IsInt = true;
                }

                properties.Add(property);
            }

            return properties;
        }

    }
}
