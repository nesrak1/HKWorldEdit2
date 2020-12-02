using AssetsTools.NET;

public class Constants
{
    public static readonly uint[] editDifferScriptNEHash = new uint[] { 0x6eb46069, 0x5eda7d6c, 0xbfd0ce46, 0xadf887b5 };
    public static readonly uint[] tk2dEmuScriptNEHash = new uint[] { 0x1675977a, 0x8b511402, 0x91c325bb, 0x2117a2e4 };
    public static readonly uint[] sceneMetadataScriptNEHash = new uint[] { 0x993f3608, 0x2cc0f87e, 0xca6371c5, 0x4ed77624 };
    //315b6d2463669124586f57520d2ce601
    public static readonly long editDifferMsEditorScriptHash = 0x4219663642d6b513;
    public static readonly long editDifferLsEditorScriptHash = 0x106ec2d02575f685;
    //9e752ce486f2c364a8d936cacc485cc4 (actually it's not lol see below)
    public static readonly long tk2dEmuMsEditorScriptHash = 0x4d0e83f044826641;
    public static readonly long tk2dEmuLsEditorScriptHash = unchecked((long)0xde056e53911a9d88);
    //9ac32aa6b7be9a74f9ba4bef659fcb97
    public static readonly long sceneMetadataMsEditorScriptHash = 0x47a9eb7b6aa23ca9;
    public static readonly long sceneMetadataLsEditorScriptHash = 0x79bcf956feb4ab9f;
}
public enum Flags
{
    None = 0x0,
    HideInEditorMask = 0x1,
    NotEditableMask = 0x10,
    StrongPPtrMask = 0x40,
    TreatIntegerValueAsBoolean = 0x100,
    DebugPropertyMask = 0x1000,
    AlignBytesFlag = 0x4000,
    AnyChildUsesAlignBytesFlag = 0x8000,
    IgnoreInMetaFiles = 0x80000,
    TransferAsArrayEntryNameInMetaFiles = 0x100000,
    TransferUsingFlowMappingStyle = 0x200000,
    GenerateBitwiseDifferences = 0x400000,
    DontAnimate = 0x800000,
    TransferHex64 = 0x1000000,
    CharPropertyMask = 0x2000000,
    DontValidateUTF8 = 0x4000000
}