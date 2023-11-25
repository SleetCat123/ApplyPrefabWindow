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
        bool addGameObjects = true;
        bool addComponents = true;
        bool removeComponents = true;
        bool objectOverrides = true;
        bool componentOverrides = true;

        GameObject selectedObj;
        GameObject rootObj;

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
        }

        public void UpdateModifiedList( ) {
            if ( selectedObj == null || rootObj == null ) {
                ClearModifiedList( );
                return;
            }
            if ( addGameObjects ) {
                addedObjectsList = PrefabUtility.GetAddedGameObjects( rootObj );
                addedObjectsList = addedObjectsList.Where( v => IsRecursiveChild( selectedObj.transform, v.instanceGameObject.transform ) );
            }
            var modifiedComponents = new List<Component>();
            if ( addComponents ) {
                addedComponentsList = PrefabUtility.GetAddedComponents( rootObj );
                addedComponentsList = addedComponentsList.Where( v => !ignoreComponents.Contains( v.instanceComponent.GetType( ).Name ) );
                addedComponentsList = addedComponentsList.Where( v => IsRecursiveChild( selectedObj.transform, v.instanceComponent.transform ) );

                modifiedComponents.AddRange( addedComponentsList.Select( v => v.instanceComponent ) );
            }
            if ( removeComponents ) {
                removedComponentsList = PrefabUtility.GetRemovedComponents( rootObj );
                removedComponentsList = removedComponentsList.Where( v => !ignoreComponents.Contains( v.assetComponent.GetType( ).Name ) );
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
                    componentsOverridesList = componentsOverridesList.Where( v => !ignoreComponents.Contains( v.instanceObject.GetType( ).Name ) );
                    componentsOverridesList = componentsOverridesList.Where( v => IsRecursiveChild( selectedObj.transform, ( v.instanceObject as Component ).transform ) );

                    modifiedComponents.AddRange( componentsOverridesList.Select( v => v.instanceObject as Component ) );
                }
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
        void OnFocus( ) {
            if ( EditorApplication.isPlaying ) {
                return;
            }
            if ( selectedObj == null || EditorUtility.IsPersistent( selectedObj ) ) {
                return;
            }
            rootObj = PrefabUtility.GetNearestPrefabInstanceRoot( selectedObj );
            if ( rootObj == null ) {
                return;
            }
            UpdateModifiedList( );
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
            bool b = GUILayout.Button( "Apply", GUILayout.Width( 50 ) );
            GUI.backgroundColor = tempColor;
            if ( !b ) {
                return false;
            }
            if ( !EditorUtility.DisplayDialog( "Apply", $"{target}をPrefabに Apply してもよろしいですか？\n\nAre you sure you want to APPLY {target} to Prefab?", "Apply", "No" ) ) {
                return false;
            }
            item.Apply( );
            UpdateModifiedList( );
            return true;

        }
        bool RevertButton( Object target, PrefabOverride item ) {
            if ( !GUILayout.Button( "Revert", GUILayout.Width( 50 ) ) ) {
                return false;
            }
            if ( !EditorUtility.DisplayDialog( "", $"{target}をRevertしてもよろしいですか？\n\nAre you sure you want to Revert {target} to Prefab?", "Yes", "No" ) ) {
                return false;
            }
            item.Revert( );
            UpdateModifiedList( );
            return true;

        }
        private void Update( ) {
            if ( EditorApplication.isPlaying ) {
                return;
            }
            var prev_selectedObj = selectedObj;
            selectedObj = Selection.activeGameObject;
            if ( prev_selectedObj != selectedObj ) {
                UpdateModifiedList( );
                Repaint( );
            }
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
            using ( new EditorGUI.DisabledScope( true ) ) {
                EditorGUILayout.ObjectField( "Selected", selectedObj, typeof( GameObject ), true );
                EditorGUILayout.ObjectField( "Root", rootObj, typeof( GameObject ), true );
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
            EditorGUILayout.LabelField( "Components:", EditorStyles.boldLabel );
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
                }
            }
            if ( componentOverrides ) {
                EditorGUILayout.LabelField( "Component Overrides:", EditorStyles.boldLabel );
                foreach ( var item in componentsOverridesList ) {
                    using ( new EditorGUILayout.HorizontalScope( ) ) {
                        using ( new EditorGUI.DisabledScope( true ) ) {
                            EditorGUILayout.ObjectField( item.instanceObject, typeof( Object ), true );
                        }
                        ApplyButton( item.instanceObject, item );
                        RevertButton( item.instanceObject, item );
                    }
                }
            }
            EditorGUILayout.EndScrollView( );

            EditorGUILayout.Separator( );
            var tempColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color( 0.6f, 0.8f, 1 );
            if ( GUILayout.Button( "Apply Children", GUILayout.Height( 50 ) ) && EditorUtility.DisplayDialog( "Apply", "子オブジェクトをPrefabに Apply してもよろしいですか？\n\nAre you sure you want to APPLY children objects to Prefab?", "Apply", "No" ) ) {
                log.Clear( );
                ModifyPrefab( ModifyMode.Apply );
            }
            GUI.backgroundColor = tempColor;
            if ( GUILayout.Button( "Revert Children", GUILayout.Height( 50 ) ) && EditorUtility.DisplayDialog( "", "子オブジェクトをRevertしてもよろしいですか？\n\nAre you sure you want to Revert children objects to Prefab?", "Yes", "No" ) ) {
                log.Clear( );
                ModifyPrefab( ModifyMode.Revert );
            }
            scroll_log = EditorGUILayout.BeginScrollView( scroll_log );
            foreach ( var item in log ) {
                EditorGUILayout.LabelField( item );
            }
            EditorGUILayout.EndScrollView( );
        }
    }
}
#endif