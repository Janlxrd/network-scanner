using NetworkScanner.Core;

namespace NetworkScanner.Tests;

public class VendorNameTests
{
    [Theory]
    [InlineData("XIAOMI Electronics,CO.,LTD", "Xiaomi")]
    [InlineData("Motorola (Wuhan) Mobility Technologies Communication Co., Ltd.", "Motorola Mobility")]
    [InlineData("Raspberry Pi Trading Ltd", "Raspberry Pi")]
    [InlineData("Raspberry Pi (Trading) Ltd", "Raspberry Pi")]
    [InlineData("Samsung Electronics Co.,Ltd", "Samsung")]
    [InlineData("D-Link International", "D-Link")]
    [InlineData("Micro-Star INTL CO., LTD.", "Micro-Star")]
    [InlineData("Earda Technologies co Ltd", "Earda")]
    [InlineData("Zhone Technologies, Inc.", "Zhone")]
    [InlineData("TCL King Electrical Appliances (Huizhou) Co., Ltd", "TCL King Electrical Appliances")]
    [InlineData("LG Electronics", "LG")]
    [InlineData("ASUSTek COMPUTER INC.", "ASUSTek Computer")]
    [InlineData("Private MAC", "Private MAC")]
    [InlineData("MAC unknown", "MAC unknown")]
    [InlineData("Co., Ltd.", "Co., Ltd.")]
    [InlineData("  Intel Corporate  ", "Intel Corporate")]
    public void ShortVendor(string input, string expected) => Assert.Equal(expected, DeviceText.ShortVendor(input));
}
