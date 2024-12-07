using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using ObjectField = UnityEditor.UIElements.ObjectField;

namespace ybwork.GameConfig.Editor
{
    internal class GameConfigWindow : EditorWindow
    {
        private static GameConfigWindow _window;
        private GameConfigData _data;

        [MenuItem("ybwork/GameConfig/GameConfigWindow")]
        public static void OpenWindow()
        {
            if (_window == null)
            {
                _window = GetWindow<GameConfigWindow>();
                if (_window == null)
                    _window = CreateInstance<GameConfigWindow>();
            }
            _window.Show();
        }

        private void Awake()
        {
            _data = GameConfigData.GetData();
            Type type = _data.Packages[0].Tables[0].Define.GetClass();
            var members = type.GetMembers()
                .Where(member => member.MemberType is MemberTypes.Property or MemberTypes.Field);
            foreach (var item in members)
            {
                Debug.Log(item.Name);
            }
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;

            // 全局搜索并加载
            string[] guids = AssetDatabase.FindAssets(nameof(GameConfigWindow));
            VisualTreeAsset treeAsset = guids
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Where(assetPath => AssetDatabase.GetMainAssetTypeAtPath(assetPath) == typeof(VisualTreeAsset))
                .Select(assetPath => AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath))
                .First();
            treeAsset.CloneTree(root);

            ListView packageListView = root.Q<ListView>("PackageListView");
            ListView tableListView = root.Q<ListView>("TableListView");
            ScrollView valueScrollView = root.Q<ScrollView>("ValueScrollView");

            TextField packageName = root.Q<TextField>("PackageName");
            packageName.SetEnabled(false);
            packageName.RegisterValueChangedCallback(evt =>
            {
                GameConfigPackageData package = _data.Packages[packageListView.selectedIndex];
                package.PackageName = evt.newValue;
                packageListView.RefreshItem(packageListView.selectedIndex);
            });

            TextField groupName = root.Q<TextField>("TableName");
            groupName.SetEnabled(false);
            groupName.RegisterValueChangedCallback(evt =>
            {
                GameConfigPackageData package = _data.Packages[packageListView.selectedIndex];
                GameConfigTableData table = package.Tables[tableListView.selectedIndex];
                table.TableName = evt.newValue;

                //RefreshCollectorList(collectorScrollView, table);
                //tableListView.RefreshItem(tableListView.selectedIndex);
            });

            packageListView.makeItem = MakeListViewItem;
            packageListView.bindItem = (element, index) =>
            {
                Label label = element.Q<Label>("Label1");
                label.text = _data.Packages[index].PackageName;
            };
            packageListView.itemsAdded += (itemIndices) =>
            {
                foreach (int index in itemIndices)
                    _data.Packages[index] = new GameConfigPackageData { PackageName = "DefaultPackage" };
                packageListView.SetSelection(_data.Packages.Count - 1);
            };
            packageListView.itemsRemoved += (itemIndices) =>
            {
                packageName.SetEnabled(false);
                packageName.SetValueWithoutNotify("");
                packageListView.SetSelection(-1);
            };
            packageListView.selectedIndicesChanged += (IEnumerable<int> indices) =>
            {
                packageName.SetEnabled(false);
                packageName.SetValueWithoutNotify("");
                tableListView.ClearSelection();

                if (packageListView.selectedIndex < 0)
                    return;

                packageName.SetEnabled(true);
                GameConfigPackageData package = _data.Packages[packageListView.selectedIndex];
                packageName.SetValueWithoutNotify(package.PackageName);

                ResetListViewDataSource(tableListView, package.Tables);
            };

            tableListView.makeItem = MakeListViewItem;
            tableListView.bindItem = (element, index) =>
            {
                Label label = element.Q<Label>("Label1");
                label.text = _data.Packages[packageListView.selectedIndex].Tables[index].TableName;
            };
            tableListView.itemsAdded += (itemIndices) =>
            {
                GameConfigPackageData package = _data.Packages[packageListView.selectedIndex];
                foreach (int index in itemIndices)
                    package.Tables[index] = new GameConfigTableData { TableName = "DefaultTable" };
                tableListView.SetSelection(package.Tables.Count - 1);
            };
            tableListView.itemsRemoved += (itemIndices) =>
            {
                groupName.SetEnabled(false);
                groupName.SetValueWithoutNotify("");
                tableListView.SetSelection(-1);
            };
            tableListView.selectedIndicesChanged += (IEnumerable<int> indices) =>
            {
                groupName.SetEnabled(false);
                groupName.SetValueWithoutNotify("");
                valueScrollView.Clear();

                GameConfigPackageData package = _data.Packages[packageListView.selectedIndex];
                GameConfigTableData table = null;
                if (package != null && tableListView.selectedIndex >= 0 && tableListView.selectedIndex < package.Tables.Count)
                    table = package.Tables[tableListView.selectedIndex];
                if (table == null)
                    return;

                groupName.SetEnabled(true);
                groupName.SetValueWithoutNotify(table.TableName);
                //RefreshCollectorList(valueScrollView, table);
            };

            TextField outputPathTextField = root.Query<TextField>("OutputPath");
            //outputPathTextField.value = _data.TargetPath;
            outputPathTextField.RegisterValueChangedCallback(value =>
            {
                //_data.TargetPath = value.newValue;
            });

            Button outputPathSelectorButton = root.Query<Button>("OutputPathSelector");
            outputPathSelectorButton.clicked += () =>
            {
                string path, name;
                if (Directory.Exists(outputPathTextField.text))
                {
                    path = Path.GetDirectoryName(outputPathTextField.text);
                    name = Path.GetFileName(outputPathTextField.text);
                }
                else
                {
                    path = Environment.CurrentDirectory;
                    name = "";
                }
                path = EditorUtility.OpenFolderPanel("选择输出路径", path, name);
                //_data.TargetPath = path;
                outputPathTextField.value = path;
            };

            // 保存按钮
            Button saveButton = root.Query<Button>("SaveButton");
            saveButton.clicked += SaveData;

            // 导出按钮
            Button buildButton = root.Query<Button>("BuildButton");
            //buildButton.clicked += Build;

            ResetListViewDataSource(packageListView, _data.Packages);
        }

        private void OnGUI()
        {
            // 检测按键事件
            if (Event.current.type == EventType.KeyDown)
            {
                // 如果按下的是Ctrl+S（在Mac上是Cmd+S）
                if ((Event.current.control || Event.current.command) && Event.current.keyCode == KeyCode.S)
                {
                    SaveData();
                    Event.current.Use();
                }
            }
        }

        private void SaveData()
        {
            //EditorUtility.SetDirty(_data);
            AssetDatabase.SaveAssets();

            Debug.Log("GameConfig保存成功");
        }

        private static void RebuildListView(ListView listView)
        {
            listView.Rebuild();
            if (listView.selectedIndex < 0 && listView.itemsSource != null && listView.itemsSource.Count > 0)
                listView.SetSelection(0);
        }

        private static void ResetListViewDataSource(ListView listView, IList source)
        {
            listView.itemsSource = source;
            RebuildListView(listView);
        }

        //private static void RefreshCollectorList(ScrollView collectorScrollView, AssetCollectorGroupData groupData)
        //{
        //    collectorScrollView.Clear();

        //    foreach (AssetCollectorItemData item in groupData.Items)
        //    {
        //        VisualElement element = MakeCollectorListViewItem(groupData, item, onDelete: () =>
        //        {
        //            groupData.Items.Remove(item);
        //            RefreshCollectorList(collectorScrollView, groupData);
        //        });
        //        collectorScrollView.Add(element);
        //    }
        //}

        private VisualElement MakeListViewItem()
        {
            VisualElement element = new VisualElement();

            {
                var label = new Label();
                label.name = "Label1";
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                label.style.flexGrow = 1f;
                label.style.height = 20f;
                label.style.marginLeft = 6f;
                element.Add(label);
            }

            return element;
        }

        //private static AssetCollectorItemData CreateCollectorItemData(Object obj)
        //{
        //    string assetPath = AssetDatabase.GetAssetPath(obj);

        //    AssetCollectorItemData collectorItemData = new AssetCollectorItemData();
        //    collectorItemData.AssetGUID = AssetDatabase.AssetPathToGUID(assetPath);
        //    return collectorItemData;
        //}

        //private static VisualElement MakeCollectorListViewItem(
        //    AssetCollectorGroupData groupData,
        //    AssetCollectorItemData collectorItemData,
        //    Action onDelete)
        //{
        //    VisualElement element = new VisualElement();

        //    VisualElement elementTop = new VisualElement();
        //    elementTop.style.flexDirection = FlexDirection.Row;
        //    element.Add(elementTop);

        //    VisualElement elementSettings = new VisualElement();
        //    elementSettings.style.flexDirection = FlexDirection.Row;
        //    elementSettings.style.height = 20;
        //    elementSettings.style.alignItems = Align.Center;
        //    element.Add(elementSettings);

        //    VisualElement elementFoldout = new VisualElement();
        //    elementFoldout.style.flexDirection = FlexDirection.Row;
        //    element.Add(elementFoldout);

        //    VisualElement elementSpace = new VisualElement();
        //    elementSpace.style.flexDirection = FlexDirection.Column;
        //    element.Add(elementSpace);

        //    // Top VisualElement
        //    {
        //        Button button = new Button();
        //        button.name = "Delete";
        //        button.text = "-";
        //        button.style.width = 20;
        //        button.style.unityTextAlign = TextAnchor.MiddleCenter;
        //        button.clicked += onDelete;
        //        elementTop.Add(button);
        //    }
        //    {
        //        VisualElement objectRow = new VisualElement();
        //        objectRow.style.flexDirection = FlexDirection.Row;
        //        objectRow.style.flexGrow = 1f;

        //        Label label = new Label();
        //        label.style.width = 65;
        //        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        //        objectRow.Add(label);

        //        ObjectField objectField = new ObjectField();
        //        objectField.name = "ObjectField1";
        //        objectField.objectType = typeof(Object);
        //        objectField.allowSceneObjects = false;
        //        objectField.style.unityTextAlign = TextAnchor.MiddleLeft;
        //        objectField.style.flexGrow = 1f;
        //        objectField.value = AssetDatabase.LoadAssetAtPath<Object>(collectorItemData.AssetPath);
        //        RefreshAssetLabel(label, collectorItemData, objectField.value);
        //        objectField.RegisterValueChangedCallback(evt =>
        //        {
        //            // register "ValuChangedEvent" to the objectField
        //            // When responding, refresh the "Main Assets" Foldout
        //            collectorItemData.AssetPath = AssetDatabase.GetAssetPath(evt.newValue);

        //            Foldout foldout = elementFoldout.Q<Foldout>("Foldout1");
        //            RefreshAssetLabel(label, collectorItemData, objectField.value);
        //            RefreshAssetList(groupData, collectorItemData, foldout);
        //        });
        //        objectRow.Add(objectField);
        //        elementTop.Add(objectRow);
        //    }

        //    // Settings VisualElement
        //    {
        //        var label = new Label();
        //        label.style.width = 93;
        //        elementSettings.Add(label);
        //    }
        //    {
        //        var label = new Label("寻址方式:");
        //        elementSettings.Add(label);
        //    }
        //    {
        //        List<string> list = new List<string> { "文件名", "分组名_文件名" };
        //        var dropdown = new DropdownField(list, 0);
        //        dropdown.style.width = 100;
        //        dropdown.index = (int)collectorItemData.AssetStyle;
        //        dropdown.RegisterValueChangedCallback(evt =>
        //        {
        //            collectorItemData.AssetStyle = (AssetCollectorItemStyle)list.IndexOf(evt.newValue);

        //            Foldout foldout = elementFoldout.Q<Foldout>("Foldout1");
        //            RefreshAssetList(groupData, collectorItemData, foldout);
        //        });
        //        elementSettings.Add(dropdown);
        //    }

        //    // Foldout VisualElement
        //    {
        //        var label = new Label();
        //        label.style.width = 90;
        //        elementFoldout.Add(label);
        //    }
        //    {
        //        Foldout foldout = new Foldout();
        //        foldout.name = "Foldout1";
        //        foldout.value = false;
        //        foldout.text = "Main Assets";
        //        elementFoldout.Add(foldout);

        //        RefreshAssetList(groupData, collectorItemData, foldout);
        //    }

        //    // Space VisualElement
        //    {
        //        var label = new Label();
        //        label.style.height = 10;
        //        elementSpace.Add(label);
        //    }

        //    return element;
        //}

        //private static void RefreshAssetList(
        //    AssetCollectorGroupData groupData,
        //    AssetCollectorItemData collectorItemData,
        //    Foldout foldout)
        //{
        //    foldout.Clear();
        //    foreach (AssetAlias assetAlias in collectorItemData.GetAssets(groupData.GroupName))
        //    {
        //        VisualElement elementItem = new VisualElement();
        //        elementItem.style.flexDirection = FlexDirection.Row;

        //        Image image = new Image();
        //        image.image = AssetDatabase.GetCachedIcon(assetAlias.AssetPath);
        //        image.style.alignSelf = Align.Center;
        //        image.uv = new Rect(0, 0, 1, 1);
        //        image.style.width = 16;
        //        image.style.height = 16;
        //        elementItem.Add(image);

        //        TextField textField = new TextField();
        //        textField.value = assetAlias.Name;
        //        textField.textEdition.isReadOnly = true;
        //        textField.style.width = 200;
        //        textField.style.minWidth = 200;
        //        textField.style.maxWidth = 200;
        //        if (collectorItemData.GetRepeatedAssets().Contains(assetAlias.Name))
        //        {
        //            textField.Children().Last().Children().First().style.color = Color.yellow;
        //        }
        //        elementItem.Add(textField);

        //        Label label = new Label();
        //        label.text = assetAlias.AssetPath;
        //        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        //        elementItem.Add(label);

        //        foldout.Add(elementItem);
        //    }
        //}
    }
}
