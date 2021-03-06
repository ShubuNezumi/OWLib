﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using STULib;
using STULib.Impl.Version2HashComparer;
using InstanceData = STULib.Impl.Version2HashComparer.InstanceData;

namespace STUHashTool {
    public class ClassBuilder {
        public InstanceData InstanceData;
        
        public ClassBuilder(InstanceData instanceData) {
            InstanceData = instanceData;
        }
        
        public static string FirstCharToUpper(string input) {
            switch (input)
            {
                case null: throw new ArgumentNullException(nameof(input));
                case "": throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input));
                default: return input.First().ToString().ToUpper() + input.Substring(1);
            }
        }

        public static string FixFieldName(string name) {
            return FirstCharToUpper(name.Substring(2)).Replace("_", "");
        }

        public static void WriteField(out string headerLine, out string contentLine, string fieldIndentString, string @namespace, 
            FieldData field, Dictionary<uint, string> instanceNames, Dictionary<uint, string> fieldNames, 
            Dictionary<uint, string> enumNames, bool properTypePaths) {
            string type = Program.GetType(field, properTypePaths);
            string fieldName = $"m_{field.Checksum:X8}";
            string fieldTypeDef = properTypePaths ? "STULib.STUField": "STUField";
            string fieldDefinition = $"[{fieldTypeDef}(0x{field.Checksum:X8}";
            if (fieldNames.ContainsKey(field.Checksum)) {
                fieldDefinition = fieldDefinition + $", \"{fieldNames[field.Checksum]}\"";
                fieldName = FixFieldName(fieldNames[field.Checksum]);
            }
            if (field.IsEmbed || field.IsEmbedArray) {
                fieldDefinition = fieldDefinition + ", EmbeddedInstance = true";
            }
            fieldDefinition = fieldDefinition + ")]";
            if (field.SerializationType == 12 || field.SerializationType == 13) {
                type = properTypePaths ? "STULib.Types.Generic.Common.STUGUID": "STUGUID";
            }
            string guidComment = "";
            if (field.IsGUIDOther || field.IsGUIDOtherArray) {
                guidComment = field.Type;
                if (instanceNames.ContainsKey(field.TypeInstanceChecksum)) guidComment = instanceNames[field.TypeInstanceChecksum];
                if (ISTU.InstanceTypes.ContainsKey(field.TypeInstanceChecksum))
                    guidComment = ISTU.InstanceTypes[field.TypeInstanceChecksum].ProperName();
                guidComment = $"  // {guidComment}";
            }
            if (field.IsInline || field.IsEmbed || field.IsEmbedArray || field.IsInlineArray) {  //  
                string instanceType = $"{@namespace}.STU_{field.TypeInstanceChecksum:X8}";
                if (instanceNames.ContainsKey(field.TypeInstanceChecksum)) {
                    instanceType = $"{@namespace}.{instanceNames[field.TypeInstanceChecksum]}";
                }
                if (ISTU.InstanceTypes.ContainsKey(field.TypeInstanceChecksum))
                    instanceType = ISTU.InstanceTypes[field.TypeInstanceChecksum].ProperName();
                headerLine = $"{fieldIndentString}{fieldDefinition}";
                contentLine = $"{fieldIndentString}public {instanceType}{(field.IsEmbedArray || field.IsInlineArray ? "[]" : "")} {fieldName};";
            } else if (type == null && !field.IsEnum && !field.IsEnumArray && !field.IsHashMap) {
                Debugger.Log(0, "STUHashTool", $"[STUHashTool:class] Unhandled type: \"{field.Type}\" (st: {field.SerializationType})\n");
                headerLine = $"{fieldIndentString}//{fieldDefinition}";
                contentLine = $"{fieldIndentString}//public object {fieldName};  // todo: unhandled type: {field.Type} (st: {field.SerializationType})";
            } else if (field.IsPrimitive || field.IsGUID || field.IsGUIDOther) {
                headerLine = $"{fieldIndentString}{fieldDefinition}";
                contentLine = $"{fieldIndentString}public {type} {fieldName};{guidComment}";
            } else if (field.IsPrimitiveArray || field.IsGUIDArray || field.IsGUIDOtherArray) {
                headerLine = $"{fieldIndentString}{fieldDefinition}";
                contentLine = $"{fieldIndentString}public {type}[] {fieldName};{guidComment}";
            } else if (field.IsHashMap) {
                string hmInstanceName = $"{@namespace}.STU_{field.HashMapChecksum:X8}";
                if (instanceNames.ContainsKey(field.HashMapChecksum)) {
                    hmInstanceName = $"{@namespace}.{instanceNames[field.HashMapChecksum]}";
                }
                if (ISTU.InstanceTypes.ContainsKey(field.HashMapChecksum)) {
                    hmInstanceName = ISTU.InstanceTypes[field.HashMapChecksum].ProperName();
                }
                headerLine = $"{fieldIndentString}{fieldDefinition}";
                string hashmapDef = properTypePaths ? "STULib.Types.Generic.Common.STUHashMap" : "STUHashMap";
                contentLine = $"{fieldIndentString}public {hashmapDef}<{hmInstanceName}> {fieldName};";
            } else if (field.IsEnum || field.IsEnumArray) {
                string enumName = $"{@namespace}.Enums.STUEnum_{field.EnumChecksum:X8}";
                if (enumNames.ContainsKey(field.EnumChecksum)) {
                    enumName = $"{@namespace}.Enums.{enumNames[field.EnumChecksum]}";
                }
                if (ISTU.EnumTypes.ContainsKey(field.EnumChecksum)) {
                    enumName = ISTU.EnumTypes[field.EnumChecksum].ProperName();
                }
                headerLine = $"{fieldIndentString}{fieldDefinition}";
                contentLine = $"{fieldIndentString}public {enumName}{(field.IsEnumArray ? "[]" : "")} {fieldName};";
            } else {
                Debugger.Log(0, "STUHashTool",
                    $"[STUHashTool:class]: Unhandled Serialization type {field.SerializationType} of field {field.Checksum:X8}\n");
                throw new Exception();  // ok this is bad now
            }
        }

        public string Build(Dictionary<uint, string> instanceNames, Dictionary<uint, string> enumNames, Dictionary<uint, string> fieldNames, string @namespace="STULib.Types", bool addUsings=false, bool properTypePaths=false) {
            StringBuilder sb = new StringBuilder();

            if (addUsings) {
                sb.AppendLine("// File auto generated by STUHashTool");
                sb.AppendLine("using static STULib.Types.Generic.Common;");
                sb.AppendLine();
            }
            sb.AppendLine($"namespace {@namespace} {{");
            const int indentLevel = 1;
            string indentString = string.Concat(Enumerable.Repeat("    ", indentLevel));
            string fieldIndentString = string.Concat(Enumerable.Repeat("    ", indentLevel + 1));
            string instanceName = $"STU_{InstanceData.Checksum:X8}";
            string parentName = $"STU_{InstanceData.ParentChecksum:X8}";
            if (instanceNames.ContainsKey(InstanceData.Checksum)) {
                instanceName = instanceNames[InstanceData.Checksum];
            }
            if (InstanceData.ParentChecksum != 0 && instanceNames.ContainsKey(InstanceData.ParentChecksum)) {
                parentName = instanceNames[InstanceData.ParentChecksum];
            }
            if (InstanceData.ParentChecksum != 0 && ISTU.InstanceTypes.ContainsKey(InstanceData.ParentChecksum)) {
                parentName = ISTU.InstanceTypes[InstanceData.ParentChecksum].ProperName();
            }
            uint fieldCounter = 1;
            
            string stuAttribute = $"STU(0x{InstanceData.Checksum:X8})]";
            if (instanceNames.ContainsKey(InstanceData.Checksum)) {
                stuAttribute = $"STU(0x{InstanceData.Checksum:X8}, \"{instanceNames[InstanceData.Checksum]}\")]";
            }
            if (properTypePaths) stuAttribute = "STULib." + stuAttribute;
            stuAttribute = "[" + stuAttribute;
            sb.AppendLine($"{indentString}{stuAttribute}");

            string instanceBaseClass = properTypePaths ? "STULib.Types.Generic.Common.STUInstance" : "STUInstance";

            sb.AppendLine(InstanceData.ParentType == null
                ? $"{indentString}public class {instanceName} : {instanceBaseClass} {{"
                : $"{indentString}public class {instanceName} : {parentName} {{");

            foreach (FieldData field in InstanceData.Fields) {
                WriteField(out string headerLine, out string contentLine, fieldIndentString, @namespace, field, 
                    instanceNames, fieldNames, enumNames, properTypePaths);
                sb.AppendLine(headerLine);
                sb.AppendLine(contentLine);

                if (fieldCounter != InstanceData.Fields.Length) {
                    sb.AppendLine();
                }
                fieldCounter++;
            }

            sb.AppendLine($"{indentString}}}");
            sb.Append("}");

            return sb.ToString();
        }
    }
}