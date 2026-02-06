using System.Collections.Generic;
using System.Xml;
using System.Globalization;
using System.Buffers.Binary;


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
                this.RegisterAddress = uint.Parse(registerNameOrAddress[2..], NumberStyles.HexNumber);
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

                //The GenICam standard allows registers to be marked with an endianess, which
                //"refers to the endianess of the device as seen trough the transport layer."
                //This appears to have been a big mistake, with version 1.0 of the schema having
                //"several limitations (or undefined behaviour)". Version 1.1 requires all registers
                //have endianess set to "correspond with the real endianess of the camera". Alas,
                //this is not the case, at least in the Teledyne DALSA Symphony device description
                //xml file. There the Bootstrap registers are big endian (as indeed they are required
                //to be for GigE Vision 2.x devices) as are a bunch of registers that don't use the
                //"device" Port. The rest are little endian. Further, endianess not only affects byte
                //order on the wire (and thus only differs between READMEM and READREG) but also bit
                //order! Meanwhile, the simulator simply stores registers in network order, ie. big
                //endian, and both READMEM and READREG simply read the register as a byte array.
                //Reading as an integer is relatively rare, is mostly just reserved for debugging, or
                //working with a bootstrap registers, and big endian is assumed.
                //So to support these crazy mixed up GenICam xml files, we'll allow the initial
                //register values defined in memory.xml (register config) to optionally be marked
                //little endian too. The value provided will then be endian swapped, ready to store
                //big endian as usual. This allows the program to continue to assume big endian every
                //where, but still allow little endian register support in the only place it matters:
                //setting initial values. The debug string for those register values will be endian
                //swapped, but that's no big deal.

                // read endianness value
                bool isLittleEndian = false; //by default
                var endianessNode = propertyNode.SelectSingleNode("endianess");
                if (endianessNode != null)
                {
                    var t = endianessNode.InnerText;
                    if (t.Contains("lit", System.StringComparison.InvariantCultureIgnoreCase) ||
                        t.Contains("small", System.StringComparison.InvariantCultureIgnoreCase) ||
                        t.Contains("true", System.StringComparison.InvariantCultureIgnoreCase) ||
                        t.Contains("1", System.StringComparison.InvariantCultureIgnoreCase))
                        isLittleEndian = true;
                }

                // read bit values
                var bitNodes = propertyNode.SelectNodes("bit");
                if (bitNodes != null && bitNodes.Count > 0)
                {
                    uint[] bits = new uint[bitNodes.Count];
                    for (int i = 0; i < bitNodes.Count; i++)
                    {
                        var bit = uint.Parse(bitNodes[i]!.InnerText);
                        if (isLittleEndian) //Okay, this is crazy. Byte **and** bit order are swapped.
                        {                   //Eg. 2 -> 5, 12 -> 11, 16 -> 23, 31 -> 24.
                            //bit order
                            bit = 31 - bit;
                            //byte order
                            if      (bit >= 24) bit -= 24;
                            else if (bit >= 16) bit -= 8;
                            else if (bit >= 8)  bit += 8;
                            else                bit += 24;
                        }
                        bits[i] = bit;
                    }

                    property.Bits = bits;
                    property.IsBits = true;
                }

                // read int value
                var intNode = propertyNode.SelectSingleNode("int");
                if (intNode != null )
                {
                    long val; //parse as long so we can accept a big uint as well as a negative int.
                    if (intNode.InnerText.StartsWith("0x"))
                        val = long.Parse(intNode.InnerText[2..], NumberStyles.HexNumber);
                    else
                        val = long.Parse(intNode.InnerText);

                    property.IntValue = isLittleEndian ? BinaryPrimitives.ReverseEndianness((int)val) : (int)val;
                    property.IsInt = true;
                }

                properties.Add(property);
            }

            return properties;
        }

    }
}
