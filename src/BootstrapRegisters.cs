
using System;
using System.Collections.Generic;

namespace GigE_Cam_Simulator
{
    public enum eBootstrapRegister
    {
        Unknown = 0,

        Version,
        Device_Mode,
        Device_MAC_address_High_Network_interface_0,
        Device_MAC_address_Low_Network_interface_0,
        Network_interface_capability_0,
        Network_interface_configuration_0,
        Current_IP_address_Network_interface_0,
        Current_subnet_mask_Network_interface_0,
        Current_default_Gateway_Network_interface_0,
        Manufacturer_name,
        Model_name,
        Device_version,
        Manufacturer_specific_information,
        Serial_number,
        User_defined_name,
        XML_Device_Description_File_First_URL,
        XML_Device_Description_File_Second_URL,
        Number_of_network_interfaces,
        Persistent_IP_address_Network_interface_0,
        Persistent_subnet_mask_Network_interface_0,
        Persistent_default_gateway_Network_interface_0,
        Link_Speed_Network_interface_0,
        Device_MAC_address_High_Network_interface_1,
        Device_MAC_address_Low_Network_interface_1,
        Network_interface_capability_1,
        Network_interface_configuration_1,
        Current_IP_address_Network_interface_1,
        Current_subnet_mask_Network_interface_1,
        Current_default_gateway_Network_interface_1,
        Persistent_IP_address_Network_interface_1,
        Persistent_subnet_mask_Network_interface_1,
        Persistent_default_gateway_Network_interface_1,
        Link_Speed_Network_interface_1,
        Device_MAC_address_High_Network_interface_2,
        Device_MAC_address_Low_Network_interface_2,
        Network_interface_capability_2,
        Network_interface_configuration_2,
        Current_IP_address_Network_interface_2,
        Current_subnet_mask_Network_interface_2,
        Current_default_gateway_Network_interface_2,
        Persistent_IP_address_Network_interface_2,
        Persistent_subnet_mask_Network_interface_2,
        Persistent_default_gateway_Network_interface_2,
        Link_Speed_Network_interface_2,
        Device_MAC_address_High_Network_interface_3,
        Device_MAC_address_Low_Network_interface_3,
        Network_interface_capability_3,
        Network_interface_configuration_3,
        Current_IP_address_Network_interface_3,
        Current_subnet_mask_Network_interface_3,
        Current_default_gateway_Network_interface_3,
        Persistent_IP_address_Network_interface_3,
        Persistent_subnet_mask_Network_interface_3,
        Persistent_default_gateway_Network_interface_3,
        Link_Speed_Network_interface_3,
        Number_of_Message_channels,
        /// <summary>
        /// This register reports the number of stream channels supported by this device.
        /// </summary>
        Number_of_Stream_channels,
        Number_of_Action_Signals,
        Action_Device_Key,
        GVSP_Capability,
        Message_channel_Capability,
        GVCP_Capability,
        Heartbeat_timeout,
        Timestamp_tick_frequency_High,
        Timestamp_tick_frequency_Low,
        Timestamp_control,
        Timestamp_value_latched_High,
        Timestamp_value_latched_Low,
        Discovery_ACK_delay,
        GVCP_Configuration,
        Pending_Timeout,
        Control_Switchover_Key,
        GVSP_Configuration,
        Physical_Link_Capability,
        Physical_Link_Configuration,
        IEEE_1588_Status,
        Scheduled_Action_Command_Queue_Size,
        IEEE_1588_Extended_Capabilities,
        IEEE_1588_Supported_Profiles,
        IEEE_1588_Selected_Profile,
        Control_Channel_Privilege,
        Primary_Application_Port,
        /// <summary>
        /// This optional register provides IP address information about the primary application holding the control channel privilege.
        /// </summary>
        Primary_Application_IP_address,
        MCP,
        MCDA,
        MCTT,
        MCRC,
        MCSP,
        Stream_Channel_Port_0,
        Stream_Channel_Packet_Size_0,
        SCPD0,
        Stream_Channel_Destination_Address_0,
        SCSP0,
        SCC0,
        SCCFG0,
        SCP1,
        SCPS1,
        SCPD1,
        SCDA1,
        SCSP1,
        SCC1,
        SCCFG1,
        SCP511,
        SCPS511,
        SCPD511,
        SCDA511,
        SCSP511,
        SCC511,
        SCCFG511,
        Manifest_Table,
        ACTION_GROUP_KEY0,
        ACTION_GROUP_MASK0,
        ACTION_GROUP_KEY1,
        ACTION_GROUP_MASK1,
        ACTION_GROUP_KEY127,
        ACTION_GROUP_MASK127,
    };

    public class BootstrapRegister
    {
        public eBootstrapRegister RegEnum { get; }
        public uint Address { get; }

        public uint Length { get; }

        public BootstrapRegister(eBootstrapRegister reg, uint address, uint length)
        {
            this.RegEnum = reg;
            this.Address = address;
            this.Length = length;
        }

        public string Name => RegEnum.ToString();
    }


    public static class BootstrapRegisterHelper
    {
        public static BootstrapRegister UnknownRegister = new BootstrapRegister(eBootstrapRegister.Unknown, 0xFFFFFFFF, 0);

        private static Dictionary<string, eBootstrapRegister> nameLookup = CreateNameLookup();

        private static BootstrapRegister[] registers = CreateRegisterInfoList();
        private static Dictionary<uint, BootstrapRegister> addressLookup = CreateAddressLookup(registers);

        public static BootstrapRegister RegisterByEnum(eBootstrapRegister reg)
        {
            var index = (int)reg;
            if ((index <= 0) || (index >= registers.Length))
            {
                return UnknownRegister;
            }

            return registers[index];
        }

        public static BootstrapRegister RegisterByAddress(uint registerAddress)
        {
            if (addressLookup.TryGetValue(registerAddress, out var info))
            {
                return info;
            }

            return UnknownRegister;
        }

        public static eBootstrapRegister RegisterEnumByName(string registerName)
        {
            if (registerName == null)
            {
                return eBootstrapRegister.Unknown;
            }

            if (nameLookup.TryGetValue(registerName, out var t))
            {
                return t;
            }
            return eBootstrapRegister.Unknown;

        }

        public static uint RegisterAddressByName(string registerName)
        {
            return RegisterByEnum(RegisterEnumByName(registerName)).Address;
        }

        private static Dictionary<uint, BootstrapRegister> CreateAddressLookup(BootstrapRegister[] registers)
        {
            var lookup = new Dictionary<uint, BootstrapRegister>();

            foreach (var info in registers)
            {
                if (info == null)
                {
                    continue;
                }
                lookup.Add(info.Address, info);
            }
            return lookup;
        }

        static Dictionary<string, eBootstrapRegister> CreateNameLookup()
        {
            var nameLookup = new Dictionary<string, eBootstrapRegister>(StringComparer.OrdinalIgnoreCase);
            var enumValues = Enum.GetValues(typeof(eBootstrapRegister));
            foreach (var enumValue in enumValues)
            {
                nameLookup.Add(enumValue.ToString() ?? "", (eBootstrapRegister)enumValue); //Redundant null check to satisfy compiler.
            }
            return nameLookup;
        }

        static BootstrapRegister[] CreateRegisterInfoList()
        {
            var values = new[]
            {
                UnknownRegister,

                // From GigE Vision Specification, version 2.2, from https://www.automate.org/vision/vision-standards/download-the-gige-vision-standard
                new BootstrapRegister(eBootstrapRegister.Version,                                                 0x0000, 4),
                new BootstrapRegister(eBootstrapRegister.Device_Mode,                                             0x0004, 4),
                new BootstrapRegister(eBootstrapRegister.Device_MAC_address_High_Network_interface_0,             0x0008, 4),
                new BootstrapRegister(eBootstrapRegister.Device_MAC_address_Low_Network_interface_0,              0x000C, 4),
                new BootstrapRegister(eBootstrapRegister.Network_interface_capability_0,                          0x0010, 4),
                new BootstrapRegister(eBootstrapRegister.Network_interface_configuration_0,                       0x0014, 4),
                new BootstrapRegister(eBootstrapRegister.Current_IP_address_Network_interface_0,                  0x0024, 4),
                new BootstrapRegister(eBootstrapRegister.Current_subnet_mask_Network_interface_0,                 0x0034, 4),
                new BootstrapRegister(eBootstrapRegister.Current_default_Gateway_Network_interface_0,             0x0044, 4),
                new BootstrapRegister(eBootstrapRegister.Manufacturer_name,                                       0x0048, 32),
                new BootstrapRegister(eBootstrapRegister.Model_name,                                              0x0068, 32),
                new BootstrapRegister(eBootstrapRegister.Device_version,                                          0x0088, 32),
                new BootstrapRegister(eBootstrapRegister.Manufacturer_specific_information,                       0x00A8, 48),
                new BootstrapRegister(eBootstrapRegister.Serial_number,                                           0x00D8, 16),
                new BootstrapRegister(eBootstrapRegister.User_defined_name,                                       0x00E8, 16),
                new BootstrapRegister(eBootstrapRegister.XML_Device_Description_File_First_URL,                   0x0200, 512),
                new BootstrapRegister(eBootstrapRegister.XML_Device_Description_File_Second_URL,                  0x0400, 512),
                new BootstrapRegister(eBootstrapRegister.Number_of_network_interfaces,                            0x0600, 4),
                new BootstrapRegister(eBootstrapRegister.Persistent_IP_address_Network_interface_0,               0x064C, 4),
                new BootstrapRegister(eBootstrapRegister.Persistent_subnet_mask_Network_interface_0,              0x065C, 4),
                new BootstrapRegister(eBootstrapRegister.Persistent_default_gateway_Network_interface_0,          0x066C, 4),
                new BootstrapRegister(eBootstrapRegister.Link_Speed_Network_interface_0,                          0x0670, 4),
                new BootstrapRegister(eBootstrapRegister.Device_MAC_address_High_Network_interface_1,             0x0680, 4),
                new BootstrapRegister(eBootstrapRegister.Device_MAC_address_Low_Network_interface_1,              0x0684, 4),
                new BootstrapRegister(eBootstrapRegister.Network_interface_capability_1,                          0x0688, 4),
                new BootstrapRegister(eBootstrapRegister.Network_interface_configuration_1,                       0x068C, 4),
                new BootstrapRegister(eBootstrapRegister.Current_IP_address_Network_interface_1,                  0x069C, 4),
                new BootstrapRegister(eBootstrapRegister.Current_subnet_mask_Network_interface_1,                 0x06AC, 4),
                new BootstrapRegister(eBootstrapRegister.Current_default_gateway_Network_interface_1,             0x06BC, 4),
                new BootstrapRegister(eBootstrapRegister.Persistent_IP_address_Network_interface_1,               0x06CC, 4),
                new BootstrapRegister(eBootstrapRegister.Persistent_subnet_mask_Network_interface_1,              0x06DC, 4),
                new BootstrapRegister(eBootstrapRegister.Persistent_default_gateway_Network_interface_1,          0x06EC, 4),
                new BootstrapRegister(eBootstrapRegister.Link_Speed_Network_interface_1,                          0x06F0, 4),
                new BootstrapRegister(eBootstrapRegister.Device_MAC_address_High_Network_interface_2,             0x0700, 4),
                new BootstrapRegister(eBootstrapRegister.Device_MAC_address_Low_Network_interface_2,              0x0704, 4),
                new BootstrapRegister(eBootstrapRegister.Network_interface_capability_2,                          0x0708, 4),
                new BootstrapRegister(eBootstrapRegister.Network_interface_configuration_2,                       0x070C, 4),
                new BootstrapRegister(eBootstrapRegister.Current_IP_address_Network_interface_2,                  0x071C, 4),
                new BootstrapRegister(eBootstrapRegister.Current_subnet_mask_Network_interface_2,                 0x072C, 4),
                new BootstrapRegister(eBootstrapRegister.Current_default_gateway_Network_interface_2,             0x073C, 4),
                new BootstrapRegister(eBootstrapRegister.Persistent_IP_address_Network_interface_2,               0x074C, 4),
                new BootstrapRegister(eBootstrapRegister.Persistent_subnet_mask_Network_interface_2,              0x075C, 4),
                new BootstrapRegister(eBootstrapRegister.Persistent_default_gateway_Network_interface_2,          0x076C, 4),
                new BootstrapRegister(eBootstrapRegister.Link_Speed_Network_interface_2,                          0x0770, 4),
                new BootstrapRegister(eBootstrapRegister.Device_MAC_address_High_Network_interface_3,             0x0780, 4),
                new BootstrapRegister(eBootstrapRegister.Device_MAC_address_Low_Network_interface_3,              0x0784, 4),
                new BootstrapRegister(eBootstrapRegister.Network_interface_capability_3,                          0x0788, 4),
                new BootstrapRegister(eBootstrapRegister.Network_interface_configuration_3,                       0x078C, 4),
                new BootstrapRegister(eBootstrapRegister.Current_IP_address_Network_interface_3,                  0x079C, 4),
                new BootstrapRegister(eBootstrapRegister.Current_subnet_mask_Network_interface_3,                 0x07AC, 4),
                new BootstrapRegister(eBootstrapRegister.Current_default_gateway_Network_interface_3,             0x07BC, 4),
                new BootstrapRegister(eBootstrapRegister.Persistent_IP_address_Network_interface_3,               0x07CC, 4),
                new BootstrapRegister(eBootstrapRegister.Persistent_subnet_mask_Network_interface_3,              0x07DC, 4),
                new BootstrapRegister(eBootstrapRegister.Persistent_default_gateway_Network_interface_3,          0x07EC, 4),
                new BootstrapRegister(eBootstrapRegister.Link_Speed_Network_interface_3,                          0x07F0, 4),
                new BootstrapRegister(eBootstrapRegister.Number_of_Message_channels,                              0x0900, 4),
                new BootstrapRegister(eBootstrapRegister.Number_of_Stream_channels,                               0x0904, 4),
                new BootstrapRegister(eBootstrapRegister.Number_of_Action_Signals,                                0x0908, 4),
                new BootstrapRegister(eBootstrapRegister.Action_Device_Key,                                       0x090C, 4),
                new BootstrapRegister(eBootstrapRegister.GVSP_Capability,                                         0x092C, 4),
                new BootstrapRegister(eBootstrapRegister.Message_channel_Capability,                              0x0930, 4),
                new BootstrapRegister(eBootstrapRegister.GVCP_Capability,                                         0x0934, 4),
                new BootstrapRegister(eBootstrapRegister.Heartbeat_timeout,                                       0x0938, 4),
                new BootstrapRegister(eBootstrapRegister.Timestamp_tick_frequency_High,                           0x093C, 4),
                new BootstrapRegister(eBootstrapRegister.Timestamp_tick_frequency_Low,                            0x0940, 4),
                new BootstrapRegister(eBootstrapRegister.Timestamp_control,                                       0x0944, 4),
                new BootstrapRegister(eBootstrapRegister.Timestamp_value_latched_High,                            0x0948, 4),
                new BootstrapRegister(eBootstrapRegister.Timestamp_value_latched_Low,                             0x094C, 4),
                new BootstrapRegister(eBootstrapRegister.Discovery_ACK_delay,                                     0x0950, 4),
                new BootstrapRegister(eBootstrapRegister.GVCP_Configuration,                                      0x0954, 4),
                new BootstrapRegister(eBootstrapRegister.Pending_Timeout,                                         0x0958, 4),
                new BootstrapRegister(eBootstrapRegister.Control_Switchover_Key,                                  0x095C, 4),
                new BootstrapRegister(eBootstrapRegister.GVSP_Configuration,                                      0x0960, 4),
                new BootstrapRegister(eBootstrapRegister.Physical_Link_Capability,                                0x0964, 4),
                new BootstrapRegister(eBootstrapRegister.Physical_Link_Configuration,                             0x0968, 4),
                new BootstrapRegister(eBootstrapRegister.IEEE_1588_Status,                                        0x096C, 4),
                new BootstrapRegister(eBootstrapRegister.Scheduled_Action_Command_Queue_Size,                     0x0970, 4),
                new BootstrapRegister(eBootstrapRegister.IEEE_1588_Extended_Capabilities,                         0x0974, 4),
                new BootstrapRegister(eBootstrapRegister.IEEE_1588_Supported_Profiles,                            0x0978, 4),
                new BootstrapRegister(eBootstrapRegister.IEEE_1588_Selected_Profile,                              0x097C, 4),
                new BootstrapRegister(eBootstrapRegister.Control_Channel_Privilege,                               0x0A00, 4), // CCP
                new BootstrapRegister(eBootstrapRegister.Primary_Application_Port,                                0x0A04, 4),
                new BootstrapRegister(eBootstrapRegister.Primary_Application_IP_address,                          0x0A14, 4),
                new BootstrapRegister(eBootstrapRegister.MCP,                                                     0x0B00, 4),
                new BootstrapRegister(eBootstrapRegister.MCDA,                                                    0x0B10, 4),
                new BootstrapRegister(eBootstrapRegister.MCTT,                                                    0x0B14, 4),
                new BootstrapRegister(eBootstrapRegister.MCRC,                                                    0x0B18, 4),
                new BootstrapRegister(eBootstrapRegister.MCSP,                                                    0x0B1C, 4),
                new BootstrapRegister(eBootstrapRegister.Stream_Channel_Port_0,                                   0x0D00, 4),
                new BootstrapRegister(eBootstrapRegister.Stream_Channel_Packet_Size_0,                            0x0D04, 4), // SCPS0
                new BootstrapRegister(eBootstrapRegister.SCPD0,                                                   0x0D08, 4),
                new BootstrapRegister(eBootstrapRegister.Stream_Channel_Destination_Address_0,                    0x0D18, 4), // SCDA0
                new BootstrapRegister(eBootstrapRegister.SCSP0,                                                   0x0D1C, 4),
                new BootstrapRegister(eBootstrapRegister.SCC0,                                                    0x0D20, 4),
                new BootstrapRegister(eBootstrapRegister.SCCFG0,                                                  0x0D24, 4),
                new BootstrapRegister(eBootstrapRegister.SCP1,                                                    0x0D40, 4),
                new BootstrapRegister(eBootstrapRegister.SCPS1,                                                   0x0D44, 4),
                new BootstrapRegister(eBootstrapRegister.SCPD1,                                                   0x0D48, 4),
                new BootstrapRegister(eBootstrapRegister.SCDA1,                                                   0x0D58, 4),
                new BootstrapRegister(eBootstrapRegister.SCSP1,                                                   0x0D5C, 4),
                new BootstrapRegister(eBootstrapRegister.SCC1,                                                    0x0D60, 4),
                new BootstrapRegister(eBootstrapRegister.SCCFG1,                                                  0x0D64, 4),
                new BootstrapRegister(eBootstrapRegister.SCP511,                                                  0x8CC0, 4),
                new BootstrapRegister(eBootstrapRegister.SCPS511,                                                 0x8CC4, 4),
                new BootstrapRegister(eBootstrapRegister.SCPD511,                                                 0x8CC8, 4),
                new BootstrapRegister(eBootstrapRegister.SCDA511,                                                 0x8CD8, 4),
                new BootstrapRegister(eBootstrapRegister.SCSP511,                                                 0x8CDC, 4),
                new BootstrapRegister(eBootstrapRegister.SCC511,                                                  0x8CE0, 4),
                new BootstrapRegister(eBootstrapRegister.SCCFG511,                                                0x8CE4, 4),
                new BootstrapRegister(eBootstrapRegister.Manifest_Table,                                          0x9000, 512),
                new BootstrapRegister(eBootstrapRegister.ACTION_GROUP_KEY0,                                       0x9800, 4),
                new BootstrapRegister(eBootstrapRegister.ACTION_GROUP_MASK0,                                      0x9804, 4),
                new BootstrapRegister(eBootstrapRegister.ACTION_GROUP_KEY1,                                       0x9810, 4),
                new BootstrapRegister(eBootstrapRegister.ACTION_GROUP_MASK1,                                      0x9814, 4),
                new BootstrapRegister(eBootstrapRegister.ACTION_GROUP_KEY127,                                     0x9FF0, 4),
                new BootstrapRegister(eBootstrapRegister.ACTION_GROUP_MASK127,                                    0x9FF4, 4),
            };

            //Previously a separate array was created and populated by using RegEnum as the index. Now
            //the redundant array has been removed, this just checks we have the same result.
            for (int i=0; i<values.Length; i++)
            {
                System.Diagnostics.Debug.Assert(i == (int)values[i].RegEnum);
            }

            return values;
        }
    }

}
