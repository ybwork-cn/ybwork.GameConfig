using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using TreeEditor;
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
        }

        ListView _packageListView;
        ListView _tableListView;
        ListView _tableContentListView;
        TextField _packageName;
        TextField _tableName;
        EnumField _tableType;
        ObjectField _typeBindElement;

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

            _packageListView = root.Q<ListView>("PackageListView");
            _tableListView = root.Q<ListView>("TableListView");
            _tableContentListView = root.Q<ListView>("TableContentListView");

            _packageName = root.Q<TextField>("PackageName");
            _packageName.SetEnabled(false);
            _packageName.RegisterValueChangedCallback(evt =>
            {
                GameConfigPackageData package = _data.Packages[_packageListView.selectedIndex];
                package.PackageName = evt.newValue;
                _packageListView.RefreshItem(_packageListView.selectedIndex);
            });

            _tableName = root.Q<TextField>("TableName");
            _tableName.SetEnabled(false);
            _tableName.RegisterValueChangedCallback(evt =>
            {
                GameConfigPackageData package = _data.Packages[_packageListView.selectedIndex];
                GameConfigTableData table = package.Tables[_tableListView.selectedIndex];
                table.TableName = evt.newValue;

                RefreshTableContent();
                _tableListView.RefreshItem(_tableListView.selectedIndex);
            });

            _tableType = root.Q<EnumField>("TableType");
            _tableType.RegisterValueChangedCallback(evt =>
            {
                GameConfigPackageData package = _data.Packages[_packageListView.selectedIndex];
                GameConfigTableData table = package.Tables[_tableListView.selectedIndex];
                table.TableType = (TableType)evt.newValue;
                RefreshTableContent();
            });
            _tableType.SetEnabled(false);

            _typeBindElement = new ObjectField("Define");
            _typeBindElement.objectType = typeof(MonoScript);
            _typeBindElement.style.marginLeft = 0;
            _typeBindElement.RegisterValueChangedCallback(evt =>
            {
                GameConfigPackageData package = _data.Packages[_packageListView.selectedIndex];
                GameConfigTableData table = package.Tables[_tableListView.selectedIndex];
                table.Define = (MonoScript)evt.newValue;
                RefreshTableContent();
            });
            _typeBindElement.SetEnabled(false);
            _tableContentListView.parent.Insert(3, _typeBindElement);

            _packageListView.makeItem = MakeListViewItem;
            _packageListView.bindItem = (element, index) =>
            {
                Label label = element.Q<Label>("Label1");
                label.text = _data.Packages[index].PackageName;
            };
            _packageListView.itemsAdded += (itemIndices) =>
            {
                foreach (int index in itemIndices)
                    _data.Packages[index] = new GameConfigPackageData { PackageName = "DefaultPackage" };
                _packageListView.SetSelection(_data.Packages.Count - 1);
            };
            _packageListView.itemsRemoved += (itemIndices) =>
            {
                _packageListView.SetSelection(-1);
                _tableListView.Rebuild();
                _tableContentListView.Rebuild();
            };
            _packageListView.selectedIndicesChanged += (IEnumerable<int> indices) =>
            {
                _packageName.SetEnabled(false);
                _packageName.SetValueWithoutNotify("");

                if (_packageListView.selectedIndex < 0)
                    return;

                GameConfigPackageData package = _data.Packages[_packageListView.selectedIndex];
                _packageName.SetEnabled(true);
                _packageName.SetValueWithoutNotify(package.PackageName);

                ResetListViewDataSource(_tableListView, package.Tables);
            };

            _tableListView.makeItem = MakeListViewItem;
            _tableListView.bindItem = (element, index) =>
            {
                if (_packageListView.selectedIndex < 0)
                    return;
                Label label = element.Q<Label>("Label1");
                label.text = _data.Packages[_packageListView.selectedIndex].Tables[index].TableName;
            };
            _tableListView.itemsAdded += (itemIndices) =>
            {
                GameConfigPackageData package = _data.Packages[_packageListView.selectedIndex];
                foreach (int index in itemIndices)
                    package.Tables[index] = new GameConfigTableData { TableName = "DefaultTable" };
                _tableListView.SetSelection(package.Tables.Count - 1);
            };
            _tableListView.itemsRemoved += (itemIndices) =>
            {
                _tableListView.SetSelection(-1);
                RefreshTableContent();
            };
            _tableListView.selectedIndicesChanged += (IEnumerable<int> indices) =>
            {
                RefreshTableContent();
            };

            _tableContentListView.makeItem = () =>
            {
                return new MyObjectField(_tableContentListView);
            };
            _tableContentListView.bindItem = (ve, index) => ((MyObjectField)ve).Bind(index);
            _tableContentListView.itemsAdded += (ids) =>
            {
                var data = _tableContentListView.itemsSource;
                foreach (var item in ids)
                {
                    data[item] = new TestData();
                }
            };
            _tableContentListView.SetEnabled(false);

            TextField outputPathTextField = root.Query<TextField>("OutputPath");
            outputPathTextField.value = _data.TargetPath;
            outputPathTextField.RegisterValueChangedCallback(value =>
            {
                _data.TargetPath = value.newValue;
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
                _data.TargetPath = path;
                outputPathTextField.value = path;
            };

            // 刷新按钮
            Button refreshButton = root.Query<Button>("RefreshButton");
            refreshButton.clicked += () =>
            {
                ResetListViewDataSource(_packageListView, _data.Packages);
                GameConfigPackageData package = _data.Packages[_packageListView.selectedIndex];
                ResetListViewDataSource(_tableListView, package.Tables);
                GameConfigTableData table = package.Tables[_tableListView.selectedIndex];
                _tableName.SetValueWithoutNotify(table.TableName);
                _tableType.SetValueWithoutNotify(table.TableType);
                RefreshTableContent();
            };

            // 保存按钮
            Button saveButton = root.Query<Button>("SaveButton");
            saveButton.clicked += SaveData;

            // 导出按钮
            Button buildButton = root.Query<Button>("BuildButton");
            //buildButton.clicked += Build;

            ResetListViewDataSource(_packageListView, _data.Packages);
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
            EditorUtility.SetDirty(_data);
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

        private void RefreshTableContent()
        {
            _tableName.SetEnabled(false);
            _tableName.SetValueWithoutNotify("");
            _typeBindElement.SetEnabled(false);
            _typeBindElement.SetValueWithoutNotify(null);

            _tableContentListView.itemsSource = null;
            _tableContentListView.Rebuild();
            _tableContentListView.SetEnabled(false);

            if (_packageListView.selectedIndex < 0)
                return;
            if (_tableListView.selectedIndex < 0)
                return;
            GameConfigPackageData package = _data.Packages[_packageListView.selectedIndex];
            GameConfigTableData table = package.Tables[_tableListView.selectedIndex];

            _tableName.SetEnabled(true);
            _tableName.SetValueWithoutNotify(table.TableName);
            _typeBindElement.SetEnabled(true);
            _typeBindElement.SetValueWithoutNotify(table.Define);

            if (table.Define != null)
            {
                Type type = table.Define.GetClass();
                IList datas = (IList)typeof(List<>)
                    .MakeGenericType(new Type[] { type })
                    .GetConstructor(new Type[] { })
                    .Invoke(new object[] { });
                foreach (var data in table.Array)
                {
                    datas.Add(JsonConvert.DeserializeObject(data, type));
                }
                _tableContentListView.itemsSource = datas;
                _tableContentListView.Rebuild();
                _tableContentListView.SetEnabled(true);
            }
        }

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
    }
}
