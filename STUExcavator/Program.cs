﻿using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DataTool;
using DataTool.Helper;
using DataTool.JSON;
using Microsoft.CSharp;
using Newtonsoft.Json;
using OWLib;
using STUHashTool;
using STULib;
using STULib.Impl.Version2HashComparer;
using STULib.Types.Generic;
using TankLib.CASC;
using Version1 = STULib.Impl.Version1;
using Version2 = STULib.Impl.Version2;
using static DataTool.Helper.Logger;
using InstanceData = STULib.Impl.Version2HashComparer.InstanceData;
using Map = STULib.Types.Map.Map;

namespace STUExcavator {
    public enum SerializationType {
        Unknown = 0,
        Raw = 1,
        // ReSharper disable once InconsistentNaming
        STUv1 = 2,
        // ReSharper disable once InconsistentNaming
        STUv2 = 3,
        MapData = 4
    }
    [JsonObject(MemberSerialization.OptOut)]
    public class AssetTypeSummary {
        public string Type;
        public SerializationType SerializationType;
        public HashSet<string> STUInstanceTypes;
        public HashSet<string> GUIDTypes;
        public bool Incomplete;
        [JsonIgnore]
        public List<Asset> Assets;
    }

    public class Asset {
        public string GUID;
        public HashSet<string> GUIDs;
        public SerializationType SerializationType;
        public HashSet<string> STUInstances;

        [JsonIgnore]
        public string JSONDump;
    }
    
    public class Program {
        public static Dictionary<ulong, ApplicationPackageManifest.Types.PackageRecord> Files;
        public static Dictionary<ushort, HashSet<ulong>> TrackedFiles;
        public static List<string> InvalidTypes;
        public static CASCConfig Config;
        public static CASCHandler CASC;
        
        public static Dictionary<uint, string> EnumNames = new Dictionary<uint, string>();
        public static Dictionary<uint, string> FieldNames = new Dictionary<uint, string>();
        public static Dictionary<uint, string> InstanceNames = new Dictionary<uint, string>();
        // dawn of a new project
        // STUExcavator:
        // go through all stu files and find GUIDs
        
        // future possibilities:
        // STUv1 excavation:
        //     find padding, then try for guid

        public static void Main(string[] args) {
            string overwatchDir = args[0];
            string outputDir = args[1];
            const string language = "enUS";
            
            // usage: "STUExcavator overwatch_dir output_dir [dataset_dir]"

            string datasetFolder = "";
            if (args.Length >= 3) {
                datasetFolder = args[2];
            }

            // casc setup
            Config = CASCConfig.LoadLocalStorageConfig(overwatchDir, false, false);
            Config.SpeechLanguage = Config.TextLanguage = language;
            CASC = CASCHandler.Open(Config);
            DataTool.Program.Files = new Dictionary<ulong, ApplicationPackageManifest.Types.PackageRecord>();
            DataTool.Program.TrackedFiles = new Dictionary<ushort, HashSet<ulong>>();
            DataTool.Program.CASC = CASC;
            DataTool.Program.Root = CASC.RootHandler;
            DataTool.Program.Flags = new ToolFlags {OverwatchDirectory = overwatchDir, Language = language};
            IO.MapCMF();
            Files = DataTool.Program.Files;
            TrackedFiles = DataTool.Program.TrackedFiles;
            
            // prepare Version2Comparer
            Version2Comparer.InstanceJSON = STUHashTool.Program.LoadInstanceJson(Path.Combine(datasetFolder, "RegisteredSTUTypes.json"));
            InvalidTypes = STUHashTool.Program.LoadInvalidTypes(Path.Combine(datasetFolder, "IgnoredBrokenSTUs.txt"));
            //STUHashTool.Program.LoadHashCSV(Path.Combine(datasetFolder, "KnownFields.csv"), out FieldNames);
            //STUHashTool.Program.LoadHashCSV(Path.Combine(datasetFolder, "KnownEnums.csv"), out EnumNames);
            //STUHashTool.Program.LoadHashCSV(Path.Combine(datasetFolder, "KnownTypes.csv"), out InstanceNames);
            FieldNames = new Dictionary<uint, string>();
            EnumNames = new Dictionary<uint, string>();
            InstanceNames = new Dictionary<uint, string>();

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings {
                Converters = new List<JsonConverter> { new GUIDArrayConverter(), new GUIDConverter() },
                PreserveReferencesHandling = PreserveReferencesHandling.All
            };
            
            // wipe ISTU
            ISTU.Clear();
            
            // actual tool
            Dictionary<string, AssetTypeSummary> types = new Dictionary<string, AssetTypeSummary>();
            CompileSTUs();
            
            foreach (KeyValuePair<ushort,HashSet<ulong>> keyValuePair in TrackedFiles.OrderBy(x => x.Key)) {
                string type = keyValuePair.Key.ToString("X3");
                if (type == "09C" || type == "062" || type == "077") continue;
                
                Log($"Processing type: {type}");
                types[type] = Excavate(keyValuePair.Key, keyValuePair.Value);
                
                IO.CreateDirectoryFromFile(Path.Combine(outputDir, type, "master.json"));
                using (Stream masterFile =
                    File.OpenWrite(Path.Combine(outputDir, type, "master.json"))) {
                    masterFile.SetLength(0);
                    string masterJson = JsonConvert.SerializeObject(types[type], Formatting.Indented);
                    using (TextWriter writer = new StreamWriter(masterFile)) {
                        writer.WriteLine(masterJson);
                    }
                }
                
                if (types[type].Assets == null) continue;
                foreach (Asset asset in types[type].Assets) {
                    string assetFile = Path.Combine(outputDir, type, "assets", $"{asset.GUID}.json");
                    string assetFileJson = Path.Combine(outputDir, type, "jsondump", $"{asset.GUID}.json");
                    IO.CreateDirectoryFromFile(assetFile);
                    IO.CreateDirectoryFromFile(assetFileJson);
                    using (Stream assetStream = File.OpenWrite(assetFile)) {
                        assetStream.SetLength(0);
                        string assetJson = JsonConvert.SerializeObject(asset, Formatting.Indented);
                        using (TextWriter writer = new StreamWriter(assetStream)) {
                            writer.WriteLine(assetJson);
                        }
                    }
                    if (asset.JSONDump == null) continue;
                    using (Stream assetStream = File.OpenWrite(assetFileJson)) {
                        assetStream.SetLength(0);
                        using (TextWriter writer = new StreamWriter(assetStream)) {
                            writer.WriteLine(asset.JSONDump);
                        }
                    }
                }
            }
        }

        public static void CompileSTUs() {
            StringBuilder sb = new StringBuilder();
            HashSet<uint> doneEnums = new HashSet<uint>();
            HashSet<uint> doneInstances = new HashSet<uint>();
            foreach (KeyValuePair<uint, STUInstanceJSON> json in Version2Comparer.InstanceJSON) {
                if (InvalidTypes.Contains(json.Value.Name)) continue;
                InstanceData instanceData = Version2Comparer.GetData(json.Key);
                if (instanceData == null) continue;
                if (doneInstances.Contains(instanceData.Checksum)) continue;
                doneInstances.Add(instanceData.Checksum);
                ClassBuilder builder = new ClassBuilder(instanceData);
                string @class = builder.Build(InstanceNames, EnumNames, FieldNames, "STUExcavator.Types", false, true);
                sb.AppendLine(@class);

                foreach (FieldData field in instanceData.Fields) {
                    if (!field.IsEnum && !field.IsEnumArray) continue;
                    if (doneEnums.Contains(field.EnumChecksum)) continue;
                    doneEnums.Add(field.EnumChecksum);
                    EnumBuilder enumBuilder = new EnumBuilder(new STUEnumData {
                        Type = STUHashTool.Program.GetSizeType(field.Size),
                        Checksum = field.EnumChecksum
                    });
                    sb.AppendLine(enumBuilder.Build(EnumNames, "STUExcavator.Types.Enums", true));
                }
            }

            CSharpCodeProvider provider = new CSharpCodeProvider();
            CompilerParameters parameters = new CompilerParameters();
            parameters.ReferencedAssemblies.Add("STULib.dll");
            parameters.ReferencedAssemblies.Add("OWLib.dll");
            parameters.GenerateInMemory = true;
            CompilerResults results = provider.CompileAssemblyFromSource(parameters, sb.ToString());

            if (results.Errors.HasErrors) {
                StringBuilder sb2 = new StringBuilder();

                foreach (CompilerError error in results.Errors) {
                    sb2.AppendLine($"Error ({error.ErrorNumber}): {error.ErrorText}");
                }

                throw new InvalidOperationException(sb2.ToString());
            }
            
            Assembly assembly = results.CompiledAssembly;
            foreach (KeyValuePair<uint, STUInstanceJSON> json in Version2Comparer.InstanceJSON) {
                if (InvalidTypes.Contains(json.Value.Name)) continue;
                Type compiledInst = assembly.GetType($"STUExcavator.Types.{json.Value.Name}");
                if (compiledInst == null) continue;
                ISTU.InstanceTypes[json.Value.Hash] = compiledInst;
            }
        }

        public static Asset Excavate(ushort type, ulong guid) {
            Asset asset = new Asset {
                GUID = IO.GetFileName(guid),
                SerializationType = SerializationType.Unknown
            };

            using (Stream file = IO.OpenFile(guid)) {
                if (file == null) return asset;
                using (BinaryReader reader = new BinaryReader(file)) {
                    if (Version1.IsValidVersion(reader)) {
                        reader.BaseStream.Position = 0;
                        asset.SerializationType = SerializationType.STUv1;
                        asset.STUInstances = new HashSet<string>();
                        asset.GUIDs = new HashSet<string>();
                        
                        reader.BaseStream.Position = 0;
                        
                        // try and auto detect padding that is before a guid
                        int maxCount = 0;
                        for (int i = 0; i < reader.BaseStream.Length; i++) {
                            byte b = reader.ReadByte();
                            if (b == 255) maxCount++;
                            if (maxCount >= 8 && b != 255) {
                                if (reader.BaseStream.Length - reader.BaseStream.Position > 8) {
                                    reader.BaseStream.Position -= 1; // before b
                                    Common.STUGUID rawGUID = new Common.STUGUID(reader.ReadUInt64());
                                    reader.BaseStream.Position -= 7; // back to after b
                                    if (GUID.Type(rawGUID) > 1) {
                                        asset.GUIDs.Add(rawGUID.ToString());
                                    }
                                }
                            } 
                            if (b != 255 && maxCount > 0) maxCount = 0;
                        }
                    } else if (type == 0xBC) {
                        asset.SerializationType = SerializationType.MapData;
                        asset.GUIDs = new HashSet<string>();
                        asset.STUInstances = new HashSet<string>();
                        reader.BaseStream.Position = 0;
                        Map map = new Map(file, uint.MaxValue);
                        foreach (ISTU stu in map.STUs) {
                            asset.GUIDs = new HashSet<string>(asset.GUIDs.Concat(GetGUIDs(stu)).ToList());
                            foreach (Common.STUInstance stuInstance in stu.Instances) {
                                STUAttribute attr = stuInstance?.GetType().GetCustomAttributes<STUAttribute>().FirstOrDefault();
                                if (attr == null) continue;
                                asset.STUInstances.Add(attr.Checksum.ToString("X8"));
                            }
                        }
                    } else if (type == 0x6) {
                        asset.SerializationType = SerializationType.Raw;
                        
                        reader.BaseStream.Position = 0x18L;
                        ulong effectKey = reader.ReadUInt64();
                        
                        reader.BaseStream.Position = 0;
                        Animation animation = new Animation(reader.BaseStream);
                        HashSet<string> guids = new HashSet<string>();
                        if (animation.Header.F08Key != 0) guids.Add(IO.GetFileName(animation.Header.F08Key));
                        if (effectKey != 0) guids.Add(IO.GetFileName(effectKey));

                        asset.GUIDs = guids;
                    } else {
                        if (Version2.IsValidVersion(reader)) {   // why is there no magic, blizz pls
                            reader.BaseStream.Position = 0;
                            Version2 stuVersion2 = null;
                            try {
                                stuVersion2 = new Version2(file, uint.MaxValue);
                                asset.SerializationType = SerializationType.STUv2;
                                asset.GUIDs = new HashSet<string>();
                                asset.STUInstances = new HashSet<string>();
                            } catch (Exception) {
                                asset.SerializationType = SerializationType.Unknown;
                            }
                            if (stuVersion2 != null) {
                                asset.GUIDs = GetGUIDs(stuVersion2);
                                // broken: todo
                                // foreach (uint typeHash in stuVersion2.TypeHashes) {
                                //     asset.STUInstances.Add(typeHash.ToString("X8"));
                                // }
                                asset.JSONDump = "";
                                StringBuilder sb = new StringBuilder();

                                int i = 0;
                                foreach (Common.STUInstance stuInstance in stuVersion2.Instances.Concat(stuVersion2.HiddenInstances)) {
                                    STUAttribute attr = stuInstance?.GetType().GetCustomAttributes<STUAttribute>().FirstOrDefault();
                                    if (attr == null) continue;
                                    asset.STUInstances.Add($"{attr.Checksum:X8}");
                                    
                                    if (stuInstance.Usage != Common.InstanceUsage.Root) continue;
                                    if (i != 0) sb.AppendLine();
                                    sb.AppendLine($"{attr.Checksum:X8}:");
                                    sb.AppendLine(JsonConvert.SerializeObject(stuInstance, Formatting.Indented));
                                    i++;
                                }
                                asset.JSONDump = sb.ToString();
                            }
                        }
                    }
                }
            }
            return asset;
        }

        public static AssetTypeSummary Excavate(ushort type, HashSet<ulong> files) {
            AssetTypeSummary summary = new AssetTypeSummary {Type = $"{type:X3}", SerializationType = SerializationType.Unknown};
            
            if (files.Count == 0) {
                summary.Incomplete = true;
                return summary;
            }
            // look at first file:
            Asset firstAsset = Excavate(type, files.First());
            if (firstAsset.SerializationType == SerializationType.Unknown) {
                summary.Incomplete = true;
                return summary;
            }
            summary.SerializationType = firstAsset.SerializationType;

            switch (summary.SerializationType) {
                case SerializationType.Raw:
                    summary.GUIDTypes = new HashSet<string>();
                    break;
                case SerializationType.MapData:
                case SerializationType.STUv1:
                case SerializationType.STUv2:
                    summary.GUIDTypes = new HashSet<string>();
                    summary.STUInstanceTypes = new HashSet<string>();
                    break;
            }
            List<Asset> assets = new List<Asset>();
            
            foreach (ulong guid in files) {
                Asset asset;
                try {
                    asset = Excavate(type, guid);
                } catch (Exception e) {
                    Console.Out.WriteLine(e);
                    continue;
                }
                assets.Add(asset);
                if (asset.GUIDs == null) continue;
                foreach (string assetGUID in asset.GUIDs) {
                    summary.GUIDTypes.Add(assetGUID.Split('.')[1]);
                }
                if (asset.STUInstances != null) {
                    foreach (string instance in asset.STUInstances) {
                        summary.STUInstanceTypes.Add(instance);
                    }
                }
                // broken: todo
                // if (asset.STUInstances != null) {
                //     foreach (string instance in asset.STUInstances) {
                //         summary.STUInstanceTypes.Add(instance);
                //     }
                // }
            }

            summary.Assets = assets;

            return summary;
        }

        public static HashSet<string> GetGUIDs(ISTU stu) {
            HashSet<string> guids = new HashSet<string>();

            IEnumerable<Common.STUInstance> instances = stu.Instances;
            if (stu.GetType() == typeof(Version2)) {
                instances = instances.Concat(((Version2) stu).HiddenInstances);
            }

            foreach (Common.STUInstance instance in instances) {
                // this means all instances, we don't need to recurse
                if (instance == null) continue;
                FieldInfo[] fields = GetFields(instance.GetType(), true);
                foreach (FieldInfo field in fields) {
                    object fieldValue = field.GetValue(instance);
                    if (fieldValue == null) continue;
                    if (field.FieldType == typeof(Common.STUGUID[])) {
                        foreach (Common.STUGUID guid in (Common.STUGUID[]) fieldValue) guids.Add(IO.GetFileName(guid));
                    }
                    if (field.FieldType == typeof(Common.STUGUID)) {
                        guids.Add(IO.GetFileName(fieldValue as Common.STUGUID));
                    }
                    
                    // tbh I haven't seen any ulong that isn't a guid yet
                    if (field.FieldType == typeof(ulong)) {
                        if (GUID.Type((ulong) fieldValue) > 1) guids.Add(IO.GetFileName(new Common.STUGUID((ulong)fieldValue)));
                    }
                    if (field.FieldType == typeof(ulong[])) {
                        foreach (ulong guid in (ulong[]) fieldValue) {
                            if (GUID.Type(guid) > 1) guids.Add(IO.GetFileName(new Common.STUGUID(guid)));
                        }
                    }
                }
            }
            return guids;
        }

        internal static FieldInfo[] GetFields(Type type, bool doParent=false) {
            FieldInfo[] parent = new FieldInfo[0];
            if (type.BaseType != null && type.BaseType.Namespace != null &&
                !type.BaseType.Namespace.StartsWith("System.") && doParent) parent = GetFields(type.BaseType);
            return parent.Concat(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                                BindingFlags.DeclaredOnly)).ToArray();
        }
    }
}