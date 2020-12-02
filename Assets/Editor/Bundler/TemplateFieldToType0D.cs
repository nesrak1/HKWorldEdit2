using AssetsTools.NET;
using System.Collections.Generic;

namespace Assets.Bundler
{
    public class TemplateFieldToType0D
    {
        public string stringTable;
        private int index;
        public TypeField_0D[] TemplateToTypeField(AssetTypeTemplateField[] fields)
        {
            return TemplateToTypeField(fields, string.Empty);
        }
        public TypeField_0D[] TemplateToTypeField(AssetTypeTemplateField[] fields, Type_0D stringTableType)
        {
            return TemplateToTypeField(fields, stringTableType.stringTable);
        }
        public TypeField_0D[] TemplateToTypeField(AssetTypeTemplateField[] fields, string stringTable)
        {
            this.stringTable = stringTable;
            index = 12; //may differ between versions so check cldb or something
            List<TypeField_0D> typeFields = new List<TypeField_0D>();
            foreach (AssetTypeTemplateField field in fields)
            {
                //start at one because when we combine this with
                //monobehaviour, we want it to be under the base field
                bool ignored;
                ReadTemplateFields(typeFields, field, 1, false, out ignored);
            }
            return typeFields.ToArray();
        }
        private void ReadTemplateFields(List<TypeField_0D> typeFields, AssetTypeTemplateField templateField, int depth, bool inString, out bool aligned)
        {
            TypeField_0D tf = new TypeField_0D();
            int tfPos = typeFields.Count;
            typeFields.Add(tf);
            tf.version = 1;
            tf.depth = (byte)depth;
            tf.isArray = (byte)(templateField.isArray ? 1 : 0);
            tf.typeStringOffset = GetStringOffset(templateField.type);
            tf.nameStringOffset = GetStringOffset(templateField.name);
            tf.index = (uint)index++;
            tf.size = GetFieldSize(templateField.valueType);
            bool anyChildAligned = false;
            bool currentlyInString = inString | templateField.valueType == EnumValueTypes.ValueType_String;
            if (templateField.childrenCount > 0)
            {
                foreach (AssetTypeTemplateField child in templateField.children)
                {
                    bool childAligned;
                    ReadTemplateFields(typeFields, child, depth + 1, currentlyInString, out childAligned);
                    anyChildAligned |= childAligned;
                }
            }

            Flags flags = Flags.None;
            flags |= templateField.align ? Flags.AlignBytesFlag : Flags.None;
            flags |= inString ? Flags.HideInEditorMask : Flags.None;
            flags |= anyChildAligned ? Flags.AnyChildUsesAlignBytesFlag : Flags.None;
            tf.flags = (uint)flags;

            typeFields[tfPos] = tf;
            aligned = templateField.align;
        }
        private int GetFieldSize(EnumValueTypes valueType)
        {
            switch (valueType)
            {
                case EnumValueTypes.ValueType_Bool:
                case EnumValueTypes.ValueType_Int8:
                case EnumValueTypes.ValueType_UInt8:
                    return 1;
                case EnumValueTypes.ValueType_Int16:
                case EnumValueTypes.ValueType_UInt16:
                    return 2;
                case EnumValueTypes.ValueType_Int32:
                case EnumValueTypes.ValueType_UInt32:
                case EnumValueTypes.ValueType_Float:
                    return 4;
                case EnumValueTypes.ValueType_Int64:
                case EnumValueTypes.ValueType_UInt64:
                case EnumValueTypes.ValueType_Double:
                    return 8;
                default:
                    return -1;
            }
        }

        private uint GetStringOffset(string str)
        {
            if (Type_0D.strTable.Contains(str))
            {
                return (uint)Type_0D.strTable.IndexOf(str) + 0x80000000;
            }
            else if (stringTable.Contains(str))
            {
                return (uint)stringTable.IndexOf(str);
            }
            else
            {
                int pos = stringTable.Length;
                stringTable += str + '\0';
                return (uint)pos;
            }
        }
    }
}
