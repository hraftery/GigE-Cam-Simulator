using System.Collections.Generic;
using System.Xml;


namespace GigE_Cam_Simulator
{
    public class PropertyItem
    {
        public string RegisterName { get; }
        public RegisterTypes Register => RegisterTypeHelper.RegisterTypeByName(this.RegisterName);
        public int RegisterAddress { get; }

        public string? StringValue { get; set; }
        public bool IsString { get; set; }

        public int IntValue { get; set; }
        public bool IsInt { get; set; }

        public int[]? Bits { get; set; }
        public bool IsBits { get; set; }


        public PropertyItem(string registerNameOrAddress)
        {
            this.RegisterName = registerNameOrAddress;

            if (registerNameOrAddress.StartsWith("0x"))
            {
                uint addr = uint.Parse(registerNameOrAddress[2..], System.Globalization.NumberStyles.HexNumber);
                //It was expected that addr would be a simple offset into the register memory space. But in the Teledyne
                //Linea camera config at least, register addresses can be very high. Specifically, they are in ranges:
                //  0x0000     - 0x9FFF     : Reserved by the spec for "Bootstrap Registers". Includes "manifest table" @ 0x9000.
                //  0x08000000 - 0x08000053 : "User set " registers
                //  0x08EFB000 - 0x08EFF003 : File access registers
                //  0x08F00000 - variable   : File access buffer
                //  0x10000020 - 0x100000FF : Transfer registers
                //  0x1200000C - 0x120003FF : Various
                //  0x18000010 - 0x18009383 : Device functions
                //  0x20000040 - 0x20003A63 : Acquisition stuff
                //  0x90000000              : pFFCCorrectionSingleOffsetReg
                //  0xA0000000              : pFFCCorrectionSingleGainReg
                //  0xB0000000              : pLUTValue_Reg
                //
                //Currently there's 68MB allocated for registers so addresses should be < 0x04400000.
                //Here we attempt to compact the address space without conflict, while leaving addresses below 0x10000
                //unaffected. But without knowing what addresses other cameras use it's very hard to make it general,
                //so this may well need to be tweaked to support other cameras. Instead of mapping addresses on the
                //fly, we could instead require the user to modify the camera.xml file to ensure all addresses are
                //within 0x04400000, but that is a burden worth trying to avoid.
                if ((addr & 0xFF0000) == 0)
                    addr = ((addr & 0xFF000000) >> 16) | (addr & 0xFFFF); //Turn 0x12005678 into 0x00125678.
                else
                    addr -= 0x6000000;                                    //Turn 0x08FF5678 into 0x02FF5678.
                this.RegisterAddress = (int)addr;
            }
            else
            {
                this.RegisterAddress = RegisterTypeHelper.RegisterByType(RegisterTypeHelper.RegisterTypeByName(registerNameOrAddress)).Address;
            }
            
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
                    int[] bits = new int[bitNodes.Count];
                    for (int i = 0; i < bitNodes.Count; i++)
                    {
                        bits[i] = int.Parse(bitNodes[i]!.InnerText);
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
