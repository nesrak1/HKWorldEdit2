using AssetsTools.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Assets.Bundler
{
    public class TypeTreeEditor
    {
        //it's probably easier to just use a stringbuilder
        //and check the front of the string by checking if null
        //but it's already built this way so *shrug*
        private Dictionary<string, uint> strTableList = new Dictionary<string, uint>();
        private Dictionary<string, uint> defTableList = new Dictionary<string, uint>();
        private uint strTablePos = 0;
        private uint defTablePos = 0;
        public Type_0D type;
        public TypeTreeEditor(Type_0D type)
        {
            NullToDict(strTableList, ref strTablePos, type.stringTable);
            NullToDict(defTableList, ref defTablePos, Type_0D.strTable);
            this.type = type;
        }

        public TypeField_0D Get(TypeField_0D parent, string field)
        {
            byte depth = parent.depth;
            uint curIndex = parent.index + 1; //assuming index is correct
            uint curDepth;
            do
            {
                TypeField_0D curField = type.typeFieldsEx[curIndex];
                curDepth = curField.depth;
                if (curDepth == depth + 1 && GetString(curField.nameStringOffset) == field)
                {
                    return curField;
                }
                curIndex++;
            } while (curDepth > depth);
            return new TypeField_0D();
        }

        public List<TypeField_0D> GetChildren(TypeField_0D parent)
        {
            List<TypeField_0D> fields = new List<TypeField_0D>();
            byte depth = parent.depth;
            uint curIndex = parent.index + 1; //assuming index is correct
            uint curDepth;
            do
            {
                TypeField_0D curField = type.typeFieldsEx[curIndex];
                curDepth = curField.depth;
                if (curDepth == depth + 1)
                {
                    fields.Add(curField);
                }
                curIndex++;
            } while (curDepth > depth);
            return fields;
        }

        public uint AddField(TypeField_0D parent, TypeField_0D field)
        {
            byte depth = parent.depth;
            uint curIndex = parent.index + 1; //assuming index is correct
            uint curDepth;
            if (curIndex < type.typeFieldsEx.Length)
            {
                do
                {
                    curDepth = type.typeFieldsEx[curIndex].depth;
                    curIndex++;
                } while (curDepth > depth && curIndex < type.typeFieldsEx.Length);
            }

            field.index = curIndex;

            uint insertIndex = curIndex;

            TypeField_0D[] dest = new TypeField_0D[type.typeFieldsEx.Length + 1];

            Array.Copy(type.typeFieldsEx, 0, dest, 0, curIndex);
            dest[curIndex] = field;
            Array.Copy(type.typeFieldsEx, curIndex, dest, curIndex + 1, type.typeFieldsEx.Length - curIndex);

            type.typeFieldsEx = dest;
            type.typeFieldsExCount++;
            curIndex++;

            while (curIndex < type.typeFieldsEx.Length)
            {
                type.typeFieldsEx[curIndex].index++;
                curIndex++;
            }
            return insertIndex;
        }

        public string GetString(uint offset)
        {
            if (offset >= 0x80000000)
            {
                return GetEmulatedNullPosition(defTableList, offset - 0x80000000);
            }
            else
            {
                return GetEmulatedNullPosition(strTableList, offset);
            }
        }

        public TypeField_0D CreateTypeField(string type, string name, byte depth, int size, uint index, bool align, bool array = false, Flags additionalFlags = Flags.None)
        {
            Flags flags = Flags.None;
            if (align)
                flags |= Flags.AlignBytesFlag;
            if (type == "bool" || type == "UInt8")
                flags |= Flags.TreatIntegerValueAsBoolean;

            flags |= additionalFlags;

            uint fieldNamePos;
            uint typeNamePos;

            if (strTableList.ContainsKey(name))
            {
                fieldNamePos = strTableList[name];
            }
            else if (defTableList.ContainsKey(name))
            {
                fieldNamePos = defTableList[name] + 0x80000000;
            }
            else
            {
                fieldNamePos = strTablePos;
                strTableList.Add(name, strTablePos);
                strTablePos += (uint)name.Length + 1;
            }

            if (strTableList.ContainsKey(type))
            {
                typeNamePos = strTableList[type];
            }
            else if (defTableList.ContainsKey(type))
            {
                typeNamePos = defTableList[type] + 0x80000000;
            }
            else
            {
                typeNamePos = strTablePos;
                strTableList.Add(type, strTablePos);
                strTablePos += (uint)type.Length + 1;
            }

            return new TypeField_0D()
            {
                depth = depth,
                flags = (uint)flags,
                index = index,
                isArray = (byte)(array ? 1 : 0),
                nameStringOffset = fieldNamePos,
                size = size,
                typeStringOffset = typeNamePos,
                version = 1
            };
        }

        public Type_0D SaveType()
        {
            DictToNull(strTableList, ref type.stringTable);
            return type;
        }

        //to solve an issue where the pointer doesn't
        //start at the beginning of the string
        private string GetEmulatedNullPosition(Dictionary<string, uint> dict, uint offset)
        {
            uint largestValue = uint.MinValue;
            string largestString = string.Empty;
            foreach (KeyValuePair<string, uint> kvp in dict)
            {
                if (kvp.Value > offset)
                {
                    break;
                }
                if (kvp.Value > largestValue)
                {
                    largestValue = kvp.Value;
                    largestString = kvp.Key;
                }
            }
            if (largestValue != uint.MinValue && largestString != string.Empty)
            {
                if (offset != largestValue)
                {
                    return largestString.Substring((int)(offset - largestValue));
                }
                else
                {
                    return largestString;
                }
            }
            else
            {
                return string.Empty;
            }
        }

        private void NullToDict(Dictionary<string, uint> dict, ref uint pos, string strTable)
        {
            string[] table = strTable.Split('\0');
            foreach (string entry in table)
            {
                if (entry != "")
                {
                    dict.Add(entry, pos);
                    pos += (uint)entry.Length + 1;
                }
            }
        }

        private void DictToNull(Dictionary<string, uint> dict, ref string strTable)
        {
            StringBuilder sb = new StringBuilder();
            List<KeyValuePair<string, uint>> sortedStrTableList = dict.OrderBy(n => n.Value).ToList();
            foreach (KeyValuePair<string, uint> entry in sortedStrTableList)
            {
                sb.Append(entry.Key + '\0');
            }
            strTable = sb.ToString();
        }
    }
}
