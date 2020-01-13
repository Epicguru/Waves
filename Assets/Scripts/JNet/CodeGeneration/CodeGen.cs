using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JNetworking.CodeGeneration
{
    public static class CodeGen
    {
        private static string Template
        {
            get
            {
                if(_nbt == null)
                {
                    _nbt = LoadTemplate("Template");
                }
                return _nbt;
            }
        }
        private static string _nbt;

        private static string[] Split
        {
            get
            {
                if(_s == null)
                {
                    _s = Template.Split('^');
                }

                return _s;
            }
        }
        private static string[] _s;

        private const int CLASS = 0;
        private const int FIELD = 1;
        private const int FIELD_UPDATE = 2;
        private const int CHECK = 3;

        private static StringBuilder str = new StringBuilder();
        private static List<FieldInfo> tempFields = new List<FieldInfo>();
        private static List<MethodInfo> tempMethods = new List<MethodInfo>();
        private static readonly Dictionary<Type, (Process read, Process write)> processors = new Dictionary<Type, (Process, Process)>() // These are the allowed types for syncvars.
        {
            { typeof(string),   (ReadBasic, WriteBasic) },
            { typeof(int),      (ReadBasic, WriteBasic) },
            { typeof(uint),     (ReadBasic, WriteBasic) },
            { typeof(float),    (ReadBasic, WriteBasic) },
            { typeof(double),   (ReadBasic, WriteBasic) },
            { typeof(bool),     (ReadBasic, WriteBasic) },
            { typeof(decimal),  (ReadBasic, WriteBasic) },
            { typeof(long),     (ReadBasic, WriteBasic) },
            { typeof(ulong),    (ReadBasic, WriteBasic) },
            { typeof(short),    (ReadBasic, WriteBasic) },
            { typeof(ushort),   (ReadBasic, WriteBasic) },
            { typeof(byte),     (ReadBasic, WriteBasic) },
            { typeof(sbyte),    (ReadBasic, WriteBasic) },
            { typeof(Vector2),  (ReadBasic, WriteBasic) },
            { typeof(Vector3),  (ReadBasic, WriteBasic) },
            { typeof(Vector4),  (ReadBasic, WriteBasic) },
            { typeof(Color),    (ReadBasic, WriteBasic) },
            { typeof(NetRef),   (ReadNetRef, WriteNetRef)}
        };

        private static string ReadNetRef(FieldInfo f, SyncVarAttribute atr)
        {
            str.Append("            target.");
            str.Append(f.Name);
            str.AppendLine(".Deserialize(msg, first);");

            return str.ToString();
        }

        private static string WriteNetRef(FieldInfo f, SyncVarAttribute atr)
        {
            str.Append("            target.").Append(f.Name).AppendLine(".Serialize(msg);");
            return str.ToString();
        }

        private delegate string Process(FieldInfo f, SyncVarAttribute atr);

#if UNITY_EDITOR
        public static void GenNetCode(Assembly a, string outputFolder, bool clear = true, bool refresh = true)
        {
            string start = "Generating netcode for " + a.GetName().Name + "... ";
            bool cleared = false;
            if(clear && Directory.Exists(outputFolder))
            {
                Directory.Delete(outputFolder, true);
                cleared = true;
            }
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            if (cleared)
            {
                UnityEditor.EditorUtility.DisplayProgressBar("Netcode Gen", start + "Refreshing database...", 0f);
                UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.Default);
            }

            UnityEditor.EditorUtility.DisplayProgressBar("Netcode Gen", start + "Getting exported types", 0f);
            List<Type> types = new List<Type>();
            Type[] allTypes = a.GetExportedTypes();
            int i = 0;

            foreach (var type in allTypes)
            {
                float p = (float)i / allTypes.Length;
                UnityEditor.EditorUtility.DisplayProgressBar("Netcode Gen", start + "Scanning exported types", p);
                i++;

                if (type.IsClass)
                {
                    if (type.IsAbstract)
                        continue;
                    if (type.IsInterface)
                        continue;
                    if (type.IsNested)
                        continue;

                    if (type.IsSubclassOf(typeof(NetBehaviour)))
                    {
                        types.Add(type);
                    }
                }                
            }

            i = 0;
            foreach (var type in types)
            {
                float p = (float)i / (types.Count * 2);
                i++;

                JNet.Log("Generating " + type.Name + "...");
                UnityEditor.EditorUtility.DisplayProgressBar("Netcode Gen", start + "Generating code for " + type.Name, p);
                string code = GenBehaviourClass(type);

                p = (float)i / (types.Count * 2);
                i++;
                UnityEditor.EditorUtility.DisplayProgressBar("Netcode Gen", start + "Saving code for " + type.Name + "_Generated.cs", p);

                string filePath = Path.Combine(outputFolder, type.Name + "_Generated.cs");
                if (File.Exists(filePath))
                    File.Delete(filePath);
                File.WriteAllText(filePath, code);                
            }

            if (refresh)
            {
                UnityEditor.EditorUtility.DisplayProgressBar("Netcode Gen", start + "Refreshing asset database", 1f);
                UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.Default);
            }            
            UnityEditor.EditorUtility.ClearProgressBar();
        }
#endif

        private static string GetSub(int num)
        {
            return Split[num];
        }

        public static string GenBehaviourClass(Type t)
        {
            // Where t is a NetBehaviour.

            // Get all sync vars.
            var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            tempFields.Clear();

            foreach (var field in fields)
            {
                if (field.GetCustomAttribute<SyncVarAttribute>() != null)
                {
                    if (!field.IsPublic)
                    {
                        JNet.Error(string.Format("SyncVar property '{0}' in {1} is not public. Syncvars must be public or protected, non static and of a valid type.", field.Name, field.DeclaringType.Name));
                        continue;
                    }

                    if (field.IsStatic)
                    {
                        JNet.Error(string.Format("SyncVar property '{0}'in {1} is static. Syncvars must be public, non static and of a valid type.", field.Name, field.DeclaringType.Name));
                        continue;
                    }
                    tempFields.Add(field);
                }
            }

            // Fields.
            str.Clear();
            string fieldsText;
            string fieldTemplate = GetSub(FIELD);
            foreach (var f in tempFields)
            {
                // Only when not a first only syncvar.
                var atr = f.GetCustomAttribute<SyncVarAttribute>();
                if (!atr.FirstOnly && f.FieldType != typeof(NetRef))
                {
                    string line = Generate(fieldTemplate, f.FieldType.FullName, "LastSent_" + f.Name).TrimEnd();
                    str.Append(line);
                }                
            }
            fieldsText = str.ToString();

            // Update method contents.
            str.Clear();
            string updateMethod;
            string updateTemplate = GetSub(FIELD_UPDATE);
            foreach (var f in tempFields)
            {
                // Only check in update when not a first only syncvar.
                var atr = f.GetCustomAttribute<SyncVarAttribute>();
                if(f.FieldType == typeof(NetRef))
                {
                    str.AppendLine("            if(target.").Append(f.Name).AppendLine(".Update())");
                    str.Append("                NetDirty = true;");
                }
                else if (!atr.FirstOnly)
                {
                    string lines = Generate(updateTemplate, f.Name, "LastSent_" + f.Name);
                    str.Append(lines);
                }
            }

            updateMethod = str.ToString();

            // Serialize
            str.Clear();
            string serializeText = "";
            foreach (var f in tempFields)
            {
                Type type = f.FieldType;
                SyncVarAttribute atr = f.GetCustomAttribute<SyncVarAttribute>();

                if (processors.ContainsKey(type))
                {
                    string o = processors[type].write(f, atr);
                    str.Clear();
                    serializeText += o;
                }
                else
                {
                    JNet.Error(string.Format("Cannot define a SyncVar {0} of type {1}. Syncvar will not function.", f.Name, type.FullName));
                }
            }

            // Deserialize
            str.Clear();
            string deserializeText = "";
            foreach (var f in tempFields)
            {
                Type type = f.FieldType;
                SyncVarAttribute atr = f.GetCustomAttribute<SyncVarAttribute>();

                if (processors.ContainsKey(type))
                {
                    string o = processors[type].read(f, atr);
                    str.Clear();
                    deserializeText += o;
                }
                else
                {
                    // Already said.
                    // JNet.Error(string.Format("Cannot define a SyncVar {0} of type {1}. Syncvar will not function.", f.Name, type.FullName));
                }
            }

            str.Clear();
            string remoteMethods = "";
            // Search methods.
            var allMethods = t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            tempMethods.Clear();
            foreach (var m in allMethods)
            {
                var rmc = m.GetCustomAttribute<RIAttribute>();
                if(rmc != null)
                {
                    if (m.IsStatic)
                    {
                        JNet.Error($"The method {t.FullName}.{m.Name}() is static, not valid as Cmd or Rpc.");
                        continue;
                    }

                    if(m.IsAbstract || m.IsVirtual)
                    {
                        JNet.Error($"The method {t.FullName}.{m.Name}() is virtual or abstract, not valid as Cmd or Rpc.");
                        continue;
                    }

                    tempMethods.Add(m);
                }
            }
            str.Append("            System.Type bt = typeof(");
            str.Append(t.FullName);
            str.AppendLine(");");
            if(tempMethods.Count > 0)
                str.AppendLine("            var binding = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;");
            int id = 0;
            foreach (var m in tempMethods)
            {
                bool isCmd = m.GetCustomAttribute<CmdAttribute>() != null;

                str.Append("            base.RemoteMethods.Add(\"");
                str.Append(m.Name.ToLower());
                str.Append("\", (bt.GetMethod(\"");
                str.Append(m.Name);
                str.Append("\", binding), ");
                str.Append(isCmd.ToString().ToLower());
                str.AppendLine("));");
                str.Append("            base.RemoteMethodMap.Add(\"");
                str.Append(m.Name.ToLower());
                str.Append("\", ");
                str.Append(id++);
                str.AppendLine(");");
            }
            remoteMethods = str.ToString();

            // Final class.
            string[] classArgs = new string[]
            {
                "JNetAutogen", // Namespace
                t.Name + "_Generated", // Class name
                fieldsText, // Class fields,
                t.FullName, // Method target type
                updateMethod, // Update method contents
                serializeText, // Serialize method contents
                deserializeText, // Deserialize method contents
                tempFields.Count.ToString(), // Number of sync vars
                remoteMethods // The remote method registering.

            };
            string classTemp = GetSub(CLASS);
            string final = Generate(classTemp, classArgs);

            return final;
        }

        private static string WriteBasic(FieldInfo f, SyncVarAttribute atr)
        {
            bool firstOnly = atr.FirstOnly;

            if (firstOnly)
            {
                str.AppendLine("            if(first)");
                str.Append("    ");
            }

            str.Append("            msg.Write(target");
            str.Append('.');
            str.Append(f.Name);
            str.AppendLine(");");

            if (!firstOnly)
            {
                str.Append("            LastSent_");
                str.Append(f.Name);
                str.Append(" = target.");
                str.Append(f.Name);
                str.AppendLine(";");
                str.AppendLine();
            }            

            return str.ToString();
        }

        private static string ReadBasic(FieldInfo f, SyncVarAttribute atr)
        {
            bool first = atr.FirstOnly;

            if(atr.Hook != null)
            {
                return ReadWithHook(f, atr.Hook, first);
            }

            if (first)
            {
                str.AppendLine("            if(first)");
                str.Append("    ");
            }

            str.Append("            target.");
            str.Append(f.Name);
            str.Append(" = msg.Read");
            str.Append(f.FieldType.Name);
            str.AppendLine("();");

            return str.ToString();
        }

        private static string ReadWithHook(FieldInfo f, string hookMethod, bool firstOnly)
        {
            if (firstOnly)
            {
                str.AppendLine("            if(first)");
                str.Append("    ");
            }

            str.Append("            target.");
            str.Append(hookMethod.Trim());
            str.Append("(msg.Read");
            str.Append(f.FieldType.Name);
            str.AppendLine("());");

            return str.ToString();
        }

        private static string LoadTemplate(string name)
        {
            TextAsset t = Resources.Load<TextAsset>("JNetResources/" + name);

            return t.text;
        }

        public static string Generate(string template, params object[] args)
        {
            string output = string.Format(template, args);
            return output;
        }

        #region Editor
#if UNITY_EDITOR
        [UnityEditor.MenuItem("Netcode/Generate #g")]
        private static void GenerateAll()
        {
            string path = Path.Combine(Application.dataPath, "NetcodeAutogen");
            var ass = AppDomain.CurrentDomain.GetAssemblies();
            int l = ass.Length; // How big is the ass?

            string names = EditorPrefs.GetString("NetcodeAssemblies", "ERROR_NOTHING");
            if(names != "ERROR_NOTHING")
            {
                List<Assembly> assList = new List<Assembly>();
                string[] realNames = names.Split(',');
                for (int i = 0; i < l; i++)
                {
                    var a = ass[i];
                    string name = a.GetName().Name;
                    if (name.StartsWith("Unity") || name.StartsWith("Microsoft") || name.StartsWith("mscorlib") || name.StartsWith("System") || name.StartsWith("Mono"))
                        continue;

                    if (realNames.Contains(name))
                    {
                        assList.Add(a);
                    }
                }
                for(int i = 0; i < assList.Count; i++)
                {
                    var a = assList[i];
                    GenNetCode(a, path, i == 0, i == assList.Count - 1);
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Netcode Error", "There are no selected assemblies to generate netcode for. Open JNet settings to fix this. Netcode > Settings menu.", "Okay");
                return;
            }            
        }

        [UnityEditor.MenuItem("Netcode/Clear Generated")]
        private static void ClearOutput()
        {
            string path = Path.Combine(Application.dataPath, "NetcodeAutogen");
            Debug.Log(path);

            if(Directory.Exists(path))
                Directory.Delete(path, true);

            string filePath = Path.Combine(Application.dataPath, "NetcodeAutogen.meta");

            if (File.Exists(filePath))
                File.Delete(filePath);

            UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.Default);

            Debug.Log("Cleared output folder.");
        }

        [UnityEditor.MenuItem("Netcode/Settings")]
        private static void ShowSettings()
        {
            Wind w = ScriptableObject.CreateInstance<Wind>();
            w.ShowUtility();
        }

        private class Wind : UnityEditor.EditorWindow
        {
            private int mask;
            private Assembly[] assemblies;
            private string[] names;

            private void RefreshAssemblies()
            {
                assemblies = AppDomain.CurrentDomain.GetAssemblies();

                List<string> tempNames = new List<string>();
                
                for (int i = 0; i < assemblies.Length; i++)
                {
                    string name = assemblies[i].GetName().Name;
                    if (name.StartsWith("Unity") || name.StartsWith("System") || name.StartsWith("mscorlib") || name.StartsWith("Mono"))
                        continue;
                    

                    tempNames.Add(name);
                }

                names = tempNames.ToArray();
            }

            private void Save()
            {
                StringBuilder str = new StringBuilder();
                List<string> toSave = new List<string>();
                bool[] flags = GetBits(mask);

                for (int i = 0; i < names.Length; i++)
                {
                    bool selected = flags[i];
                    if (selected)
                    {
                        toSave.Add(names[i]);
                    }
                }

                for(int i = 0; i < toSave.Count; i++)
                {
                    str.Append(toSave[i]);
                    if(i != toSave.Count - 1)
                        str.Append(',');
                }

                EditorPrefs.SetString("NetcodeAssemblies", str.ToString());
            }

            private bool[] GetBits(int mask)
            {
                int length = sizeof(int) * 8;

                bool[] b = new bool[length];
                for (int i = 0; i < length; i++)
                {
                    b[i] = GetBit(mask, i);
                }

                return b;
            }

            /// <summary>
            /// Gets a bit from the mask, where 0 is the least significant bit and 31 is the most significant.
            /// </summary>
            private bool GetBit(int mask, int bit)
            {
                if(bit >= 0 && bit < 32)
                {
                    int shifted = mask >> bit;
                    int final = shifted & 1;

                    return final == 1;
                }
                else
                {
                    return false;
                }
            }

            private void OnGUI()
            {
                base.maxSize = new Vector2(250, 150);
                base.minSize = base.maxSize;
                titleContent = new GUIContent("JNet settings");
                EditorGUILayout.LabelField("Included assemblies:");

                if (assemblies == null)
                    RefreshAssemblies();

                mask = EditorPrefs.GetInt("NetcodeAssembliesMask");
                mask = EditorGUILayout.MaskField(mask, names);
                EditorPrefs.SetInt("NetcodeAssembliesMask", mask);

                if(mask == 0)
                    EditorGUILayout.HelpBox("No assemblies selected! Netcode gen will not work!", MessageType.Error, true);

                if (GUILayout.Button("Save"))
                    Save();

                if (GUILayout.Button("Close"))
                {
                    Save();
                    Close();
                }
            }
        }
#endif
        #endregion
    }
}
