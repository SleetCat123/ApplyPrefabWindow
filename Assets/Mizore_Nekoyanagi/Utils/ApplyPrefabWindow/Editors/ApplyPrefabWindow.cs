#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;
using UnityEditor.SceneManagement;

namespace MizoreNekoyanagi.PublishUtil.ApplyPrefab {
    public class ApplyPrefabWindow : EditorWindow {
        Vector2 scroll;
        Vector2 scroll_log;
        List<string> log = new List<string>();
        List<Component> firstComponents = new List<Component>();
        HashSet<string> ignoreComponents = new HashSet<string>();
        IEnumerable<AddedGameObject> addedObjectsList = new List<AddedGameObject>();
        IEnumerable<AddedComponent> addedComponentsList = new List<AddedComponent>();
        IEnumerable<RemovedComponent> removedComponentsList = new List<RemovedComponent>();
        IEnumerable<ObjectOverride> objectsOverridesList = new List<ObjectOverride>();
        IEnumerable<ObjectOverride> componentsOverridesList = new List<ObjectOverride>();
        Dictionary<Object, List<PropertyModification>> propertyModifications = new Dictionary<Object, List<PropertyModification>>();
        bool addGameObjects = true;
        bool addComponents = true;
        bool removeComponents = true;
        bool objectOverrides = true;
        bool componentOverrides = true;

        bool confirmWhenApply = true;
        bool confirmWhenRevert = true;

        GameObject selectedObj;
        GameObject rootObj;
        PrefabAssetType prefabType;
        string prefabTypeStr;
        bool isOverwritable;


        [MenuItem( "Mizore/Apply Prefab Window" )]
        public static void ShowWindow( ) {
            var window = (ApplyPrefabWindow)EditorWindow.GetWindow(typeof(ApplyPrefabWindow));
            window.titleContent = new GUIContent( "Apply Prefab Window" );
            window.Show( );
        }
        public void ClearModifiedList( ) {
            firstComponents.Clear( );
            addedObjectsList = new List<AddedGameObject>( );
            addedComponentsList = new List<AddedComponent>( );
            removedComponentsList = new List<RemovedComponent>( );
            objectsOverridesList = new List<ObjectOverride>( );
            componentsOverridesList = new List<ObjectOverride>( );
            propertyModifications.Clear( );
        }

        public void UpdateModifiedList( ) {
            if ( selectedObj == null || rootObj == null ) {
                ClearModifiedList( );
                return;
            }
            prefabType = PrefabUtility.GetPrefabAssetType( rootObj );
            prefabTypeStr = prefabType.ToString( );
            isOverwritable = prefabType == PrefabAssetType.Variant || prefabType == PrefabAssetType.Regular;
            if ( addGameObjects ) {
                addedObjectsList = PrefabUtility.GetAddedGameObjects( rootObj );
                addedObjectsList = addedObjectsList.Where( v => IsRecursiveChild( selectedObj.transform, v.instanceGameObject.transform ) );
            }
            var modifiedComponents = new List<Component>();
            if ( addComponents ) {
                addedComponentsList = PrefabUtility.GetAddedComponents( rootObj );
                addedComponentsList = addedComponentsList.Where( v => IsRecursiveChild( selectedObj.transform, v.instanceComponent.transform ) );

                modifiedComponents.AddRange( addedComponentsList.Select( v => v.instanceComponent ) );
            }
            if ( removeComponents ) {
                removedComponentsList = PrefabUtility.GetRemovedComponents( rootObj );
                removedComponentsList = removedComponentsList.Where( v => IsRecursiveChild( selectedObj.transform, v.containingInstanceGameObject.transform ) );

                modifiedComponents.AddRange( removedComponentsList.Select( v => v.assetComponent ) );
            }
            if ( objectOverrides || componentOverrides ) {
                var list =  PrefabUtility.GetObjectOverrides( rootObj );
                if ( objectOverrides ) {
                    objectsOverridesList = list.Where( v => v.instanceObject is GameObject );
                    objectsOverridesList = objectsOverridesList.Where( v => IsRecursiveChild( selectedObj.transform, ( v.instanceObject as GameObject ).transform ) );
                }
                if ( componentOverrides ) {
                    componentsOverridesList = list.Where( v => v.instanceObject is Component );
                    componentsOverridesList = componentsOverridesList.Where( v => IsRecursiveChild( selectedObj.transform, ( v.instanceObject as Component ).transform ) );

                    modifiedComponents.AddRange( componentsOverridesList.Select( v => v.instanceObject as Component ) );
                }
            }
            var modifications = PrefabUtility.GetPropertyModifications( rootObj );
            propertyModifications.Clear( );
            foreach ( var item in modifications ) {
                List<PropertyModification> list;
                if ( !propertyModifications.TryGetValue( item.target, out list ) ) {
                    list = new List<PropertyModification>( );
                    propertyModifications.Add( item.target, list );
                }
                list.Add( item );
            }

            firstComponents.Clear( );
            foreach ( var item in modifiedComponents ) {
                if ( !firstComponents.Any( v => v.GetType( ) == item.GetType( ) ) ) {
                    firstComponents.Add( item );
                }
            }
            firstComponents.Sort( ( a, b ) => Comparer<string>.Default.Compare( a.GetType( ).Name, b.GetType( ).Name ) );
        }

        static bool IsRecursiveChild( Transform root, Transform t ) {
            if ( root == t ) {
                return true;
            }
            Transform temp = t;
            while ( temp != null ) {
                temp = temp.parent;
                if ( temp == root ) {
                    return true;
                }
            }
            return false;
        }
        enum ModifyMode {
            Apply, Revert
        }
        void ModifyPrefab( ModifyMode mode ) {
            if ( mode == ModifyMode.Apply ) {
                var prefabFilrPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot( rootObj );
                Debug.Log( "Prefab FilrPath: " + prefabFilrPath );
                log.Add( "Apply Start: " + selectedObj );
                log.Add( "Prefab FilrPath: " + prefabFilrPath );
            } else {
                log.Add( "Revert Start: " + selectedObj );
            }
            UpdateModifiedList( );
            if ( addGameObjects ) {
                log.Add( "- AddGameObjects" );
                foreach ( var item in addedObjectsList ) {
                    log.Add( item.instanceGameObject.ToString( ) );
                    if ( mode == ModifyMode.Apply ) {
                        item.Apply( );
                    } else {
                        item.Revert( );
                    }
                }
                log.Add( "" );
            }
            if ( addComponents ) {
                log.Add( "- AddComponents" );
                foreach ( var item in addedComponentsList ) {
                    if ( ignoreComponents.Contains( item.instanceComponent.GetType( ).Name ) ) {
                        continue;
                    }
                    log.Add( item.instanceComponent.GetType( ).ToString( ) );
                    if ( mode == ModifyMode.Apply ) {
                        item.Apply( );
                    } else {
                        item.Revert( );
                    }
                }
                log.Add( "" );
            }
            if ( removeComponents ) {
                log.Add( "- RemoveComponents" );
                foreach ( var item in removedComponentsList ) {
                    if ( ignoreComponents.Contains( item.assetComponent.GetType( ).Name ) ) {
                        continue;
                    }
                    log.Add( item.assetComponent.GetType( ).ToString( ) );
                    if ( mode == ModifyMode.Apply ) {
                        item.Apply( );
                    } else {
                        item.Revert( );
                    }
                }
                log.Add( "" );
            }
            if ( objectOverrides ) {
                log.Add( "- ObjectOverrides" );
                foreach ( var item in objectsOverridesList ) {
                    log.Add( item.instanceObject.ToString( ) );
                    if ( mode == ModifyMode.Apply ) {
                        item.Apply( );
                    } else {
                        item.Revert( );
                    }
                }
                log.Add( "" );
            }
            if ( componentOverrides ) {
                log.Add( "- ComponentOverrides" );
                foreach ( var item in componentsOverridesList ) {
                    if ( ignoreComponents.Contains( item.instanceObject.GetType( ).Name ) ) {
                        continue;
                    }
                    log.Add( item.instanceObject.ToString( ) );
                    if ( mode == ModifyMode.Apply ) {
                        item.Apply( );
                    } else {
                        item.Revert( );
                    }
                }
                log.Add( "" );
            }
            if ( mode == ModifyMode.Apply ) {
                Debug.Log( "Apply Finished" );
                log.Add( "Apply Finished" );
            } else {
                Debug.Log( "Revert Finished" );
                log.Add( "Revert Finished" );
            }
            UpdateModifiedList( );
        }
        bool ApplyButton( Object target, PrefabOverride item ) {
            var tempColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color( 0.6f, 0.8f, 1 );
            using ( new EditorGUI.DisabledGroupScope( !isOverwritable ) ) {
                bool b = GUILayout.Button( "Apply", GUILayout.Width( 50 ) );
                GUI.backgroundColor = tempColor;
                if ( !b ) {
                    return false;
                }
            }
            if ( confirmWhenApply ) {
                if ( !EditorUtility.DisplayDialog( "Apply", $"以下の値をPrefabに Apply してもよろしいですか？\nAre you sure you want to APPLY value to Prefab?\n\n{target}", "Apply", "No" ) ) {
                    return false;
                }
            }
            item.Apply( );
            UpdateModifiedList( );
            return true;

        }
        bool RevertButton( Object target, PrefabOverride item ) {
            if ( !GUILayout.Button( "Revert", GUILayout.Width( 50 ) ) ) {
                return false;
            }
            if ( confirmWhenRevert ) {
                if ( !EditorUtility.DisplayDialog( "", $"[{target}]をRevertしてもよろしいですか？\nAre you sure you want to Revert value?\n\n{target}", "Yes", "No" ) ) {
                    return false;
                }
            }
            item.Revert( );
            UpdateModifiedList( );
            return true;
        }
        bool ApplyButton( SerializedProperty prop, string prefabPath ) {
            var tempColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color( 0.6f, 0.8f, 1 );
            using ( new EditorGUI.DisabledGroupScope( !isOverwritable ) ) {
                bool b = GUILayout.Button( "Apply", GUILayout.Width( 50 ) );
                GUI.backgroundColor = tempColor;
                if ( !b ) {
                    return false;
                }
            }
            if ( confirmWhenApply ) {
                var objName = prop.serializedObject.targetObject.name;
                if ( !EditorUtility.DisplayDialog( "Apply", $"以下の値をPrefabに Apply してもよろしいですか？\nAre you sure you want to APPLY value to Prefab?\n\n{objName}.{prop.name}", "Apply", "No" ) ) {
                    return false;
                }
            }
            PrefabUtility.ApplyPropertyOverride( prop, prefabPath, InteractionMode.UserAction );
            UpdateModifiedList( );
            return true;

        }
        bool RevertButton( SerializedProperty prop ) {
            if ( !GUILayout.Button( "Revert", GUILayout.Width( 50 ) ) ) {
                return false;
            }
            if ( confirmWhenRevert ) {
                var objName = prop.serializedObject.targetObject.name;
                if ( !EditorUtility.DisplayDialog( "", $"以下の値をRevertしてもよろしいですか？\nAre you sure you want to Revert value?\n\n{objName}.{prop.name}", "Yes", "No" ) ) {
                    return false;
                }
            }
            PrefabUtility.RevertPropertyOverride( prop, InteractionMode.UserAction );
            UpdateModifiedList( );
            return true;
        }
        void OnSelectionChange( ) {
            if ( EditorApplication.isPlaying ) {
                return;
            }
            var prev_selectedObj = selectedObj;
            selectedObj = Selection.activeGameObject;
            if ( prev_selectedObj != selectedObj ) {
                if ( selectedObj != null ) {
                    rootObj = PrefabUtility.GetNearestPrefabInstanceRoot( selectedObj );
                }
                UpdateModifiedList( );
                Repaint( );
            }
        }
        void OnHierarchyChange( ) {
            UpdateModifiedList( );
            Repaint( );
        }
        private void OnFocus( ) {
            UpdateModifiedList( );
        }
        private void OnGUI( ) {
            if ( EditorApplication.isPlaying ) {
                EditorGUILayout.HelpBox( "Playモード中は使用できません。\nCannot be used during Play mode.", MessageType.Warning );
                return;
            }
            if ( selectedObj == null || EditorUtility.IsPersistent( selectedObj ) ) {
                EditorGUILayout.HelpBox( "シーン上にあるGameObjectを選択してください。\nSelect a GameObject on the scene.", MessageType.Warning );
                return;
            }
            if ( rootObj == null ) {
                EditorGUILayout.HelpBox( "prefabに属しているオブジェクトを選択してください。\nSelect the objects belonging to the prefab.", MessageType.Warning );
                return;
            }
            using ( new EditorGUILayout.HorizontalScope( ) ) {
                EditorGUILayout.PrefixLabel( "Root" );
                var icon = AssetPreview.GetMiniThumbnail( rootObj );
                var rect = EditorGUILayout.GetControlRect( );
                if ( GUI.Button( rect, new GUIContent( rootObj.name, icon ), EditorStyles.objectField ) ) {
                    EditorGUIUtility.PingObject( rootObj );
                    Selection.activeObject = rootObj;
                }
            }
            var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot( rootObj );
            using ( new EditorGUILayout.HorizontalScope( ) ) {
                EditorGUILayout.PrefixLabel( "Prefab" );
                if ( GUILayout.Button( prefabPath, EditorStyles.objectField ) ) {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>( prefabPath );
                    EditorGUIUtility.PingObject( prefab );
                }
            }
            EditorGUILayout.LabelField( "Prefab Type", prefabTypeStr );
            EditorGUILayout.Separator( );
            using ( new EditorGUILayout.HorizontalScope( ) ) {
                EditorGUILayout.PrefixLabel( "Selected" );
                var icon = AssetPreview.GetMiniThumbnail( selectedObj );
                var rect = EditorGUILayout.GetControlRect( );
                if ( GUI.Button( rect, new GUIContent( selectedObj.name, icon ), EditorStyles.objectField ) ) {
                    EditorGUIUtility.PingObject( selectedObj );
                }
            }
            if ( !isOverwritable ) {
                EditorGUILayout.HelpBox( "prefabファイルとして保存されていないオブジェクトはApplyできません。", MessageType.Warning );
            }
            EditorGUI.BeginChangeCheck( );
            addGameObjects = EditorGUILayout.Toggle( "Add GameObjects", addGameObjects );
            addComponents = EditorGUILayout.Toggle( "Add Components", addComponents );
            removeComponents = EditorGUILayout.Toggle( "Remove Components", removeComponents );
            objectOverrides = EditorGUILayout.Toggle( "Object Overrides", objectOverrides );
            componentOverrides = EditorGUILayout.Toggle( "Component Overrides", componentOverrides );
            if ( EditorGUI.EndChangeCheck( ) ) {
                UpdateModifiedList( );
            }

            EditorGUILayout.Separator( );
            scroll = EditorGUILayout.BeginScrollView( scroll );
            using ( new EditorGUILayout.HorizontalScope( ) ) {
                EditorGUILayout.LabelField( "Components:", EditorStyles.boldLabel );
                if ( ignoreComponents.Count != 0 ) {
                    if ( GUILayout.Button( "Reset Selection", GUILayout.Width( 100 ) ) ) {
                        ignoreComponents.Clear( );
                    }
                }
            }
            foreach ( var item in firstComponents ) {
                var rect = EditorGUILayout.GetControlRect();
                var width = rect.width;

                var icon = AssetPreview.GetMiniThumbnail(item);
                rect.width = 15;
                GUI.DrawTexture( rect, icon );

                rect.x += rect.width;
                rect.width = width - rect.width;
                var componentName = item.GetType( ).Name;
                EditorGUI.BeginChangeCheck( );
                var b = EditorGUI.Toggle( rect, componentName, !ignoreComponents.Contains( componentName ) );
                if ( EditorGUI.EndChangeCheck( ) ) {
                    if ( b ) {
                        if ( ignoreComponents.Contains( componentName ) ) {
                            ignoreComponents.Remove( componentName );
                        }
                    } else {
                        ignoreComponents.Add( componentName );
                    }
                }
            }
            EditorGUILayout.Separator( );

            confirmWhenApply = EditorGUILayout.Toggle( "Confirm When Apply", confirmWhenApply );
            confirmWhenRevert = EditorGUILayout.Toggle( "Confirm When Revert", confirmWhenRevert );
            if ( addGameObjects ) {
                EditorGUILayout.LabelField( "Added GameObjects:", EditorStyles.boldLabel );
                foreach ( var item in addedObjectsList ) {
                    using ( new EditorGUILayout.HorizontalScope( ) ) {
                        using ( new EditorGUI.DisabledScope( true ) ) {
                            EditorGUILayout.ObjectField( item.instanceGameObject, typeof( GameObject ), true );
                        }
                        ApplyButton( item.instanceGameObject, item );
                        RevertButton( item.instanceGameObject, item );
                    }
                }
            }
            if ( addComponents ) {
                EditorGUILayout.LabelField( "Added Components:", EditorStyles.boldLabel );
                foreach ( var item in addedComponentsList ) {
                    if ( ignoreComponents.Contains( item.instanceComponent.GetType( ).Name ) ) {
                        continue;
                    }
                    using ( new EditorGUILayout.HorizontalScope( ) ) {
                        using ( new EditorGUI.DisabledScope( true ) ) {
                            EditorGUILayout.ObjectField( item.instanceComponent, typeof( Component ), true );
                        }
                        ApplyButton( item.instanceComponent, item );
                        RevertButton( item.instanceComponent, item );
                    }
                }
            }
            if ( removeComponents ) {
                EditorGUILayout.LabelField( "Removed Components:", EditorStyles.boldLabel );
                foreach ( var item in removedComponentsList ) {
                    if ( ignoreComponents.Contains( item.assetComponent.GetType( ).Name ) ) {
                        continue;
                    }
                    using ( new EditorGUILayout.HorizontalScope( ) ) {
                        using ( new EditorGUI.DisabledScope( true ) ) {
                            EditorGUILayout.ObjectField( item.assetComponent, typeof( Component ), true );
                        }
                        ApplyButton( item.assetComponent, item );
                        RevertButton( item.assetComponent, item );
                    }
                }
            }
            if ( objectOverrides ) {
                EditorGUILayout.LabelField( "Object Overrides:", EditorStyles.boldLabel );
                foreach ( var item in objectsOverridesList ) {
                    using ( new EditorGUILayout.HorizontalScope( ) ) {
                        using ( new EditorGUI.DisabledScope( true ) ) {
                            EditorGUILayout.ObjectField( item.instanceObject, typeof( GameObject ), true );
                        }
                        ApplyButton( item.instanceObject, item );
                        RevertButton( item.instanceObject, item );
                    }
                    var original = item.GetAssetObject( );
                    EditorGUI.indentLevel++;
                    List<PropertyModification> list;
                    if ( propertyModifications.TryGetValue( original, out list ) ) {
                        foreach ( var modify in list ) {
                            var originalSerializedObject  = new SerializedObject( original );
                            var originalProp = originalSerializedObject.FindProperty(modify.propertyPath);
                            var itemSerializedObject = new SerializedObject( item.instanceObject );
                            var itemProp = itemSerializedObject.FindProperty( modify.propertyPath );
                            using ( new EditorGUILayout.HorizontalScope( ) ) {
                                EditorGUILayout.LabelField( new GUIContent( modify.propertyPath, modify.propertyPath ) );
                                EditorGUI.BeginDisabledGroup( true );
                                EditorGUILayout.PropertyField( originalProp, GUIContent.none, true );
                                EditorGUILayout.LabelField( " -> ", GUILayout.Width( 50 ) );
                                EditorGUILayout.PropertyField( itemProp, GUIContent.none, true );
                                EditorGUI.EndDisabledGroup( );
                                ApplyButton( itemProp, prefabPath );
                                RevertButton( itemProp );
                            }
                        }
                    }
                    EditorGUI.indentLevel--;
                }
            }
            if ( componentOverrides ) {
                EditorGUILayout.LabelField( "Component Overrides:", EditorStyles.boldLabel );
                foreach ( var item in componentsOverridesList ) {
                    if ( ignoreComponents.Contains( item.instanceObject.GetType( ).Name ) ) {
                        continue;
                    }
                    using ( new EditorGUILayout.HorizontalScope( ) ) {
                        using ( new EditorGUI.DisabledScope( true ) ) {
                            EditorGUILayout.ObjectField( item.instanceObject, typeof( Object ), true );
                        }
                        ApplyButton( item.instanceObject, item );
                        RevertButton( item.instanceObject, item );
                    }
                    var original = item.GetAssetObject( );
                    EditorGUI.indentLevel++;
                    List<PropertyModification> list;
                    if ( propertyModifications.TryGetValue( original, out list ) ) {
                        foreach ( var modify in list ) {
                            var originalSerializedObject  = new SerializedObject( original );
                            var originalProp = originalSerializedObject.FindProperty(modify.propertyPath);
                            var itemSerializedObject = new SerializedObject( item.instanceObject );
                            var itemProp = itemSerializedObject.FindProperty( modify.propertyPath );
                            using ( new EditorGUILayout.HorizontalScope( ) ) {
                                EditorGUILayout.LabelField( new GUIContent( modify.propertyPath, modify.propertyPath ) );
                                EditorGUI.BeginDisabledGroup( true );
                                EditorGUILayout.PropertyField( originalProp, GUIContent.none, true );
                                EditorGUILayout.LabelField( " -> ", GUILayout.Width( 50 ) );
                                EditorGUILayout.PropertyField( itemProp, GUIContent.none, true );
                                EditorGUI.EndDisabledGroup( );
                                ApplyButton( itemProp, prefabPath );
                                RevertButton( itemProp );
                            }
                        }
                    }
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndScrollView( );

            EditorGUILayout.Separator( );
            if ( !isOverwritable ) {
                EditorGUILayout.HelpBox( "prefabファイルとして保存されていないオブジェクトはApplyできません。", MessageType.Warning );
            }
            var tempColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color( 0.6f, 0.8f, 1 );
            using ( new EditorGUI.DisabledGroupScope( !isOverwritable ) ) {
                if ( GUILayout.Button( "Apply Children", GUILayout.Height( 50 ) ) && EditorUtility.DisplayDialog( "Apply", "子オブジェクトをPrefabに Apply してもよろしいですか？\n\nAre you sure you want to APPLY children objects to Prefab?", "Apply", "No" ) ) {
                    log.Clear( );
                    ModifyPrefab( ModifyMode.Apply );
                }
            }
            GUI.backgroundColor = tempColor;
            if ( GUILayout.Button( "Revert Children", GUILayout.Height( 50 ) ) && EditorUtility.DisplayDialog( "", "子オブジェクトをRevertしてもよろしいですか？\n\nAre you sure you want to Revert children objects to Prefab?", "Yes", "No" ) ) {
                log.Clear( );
                ModifyPrefab( ModifyMode.Revert );
            }
            scroll_log = EditorGUILayout.BeginScrollView( scroll_log, GUILayout.MaxHeight( 250 ) );
            foreach ( var item in log ) {
                EditorGUILayout.LabelField( item );
            }
            EditorGUILayout.EndScrollView( );
        }
    }
}
#endif