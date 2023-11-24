#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MizoreNekoyanagi.PublishUtil.ApplyPrefab {
    public class ApplyPrefabWindow : EditorWindow {
        Vector2 scroll;
        List<string> log = new List<string>();
        bool addGameObjects = true;
        bool addComponents = true;
        bool removeComponents = true;
        bool objectOverrides = true;

        [MenuItem( "Mizore/Apply Prefab Window" )]
        public static void ShowWindow( ) {
            var window = (ApplyPrefabWindow)EditorWindow.GetWindow(typeof(ApplyPrefabWindow));
            window.titleContent = new GUIContent( "Apply Prefab Window" );
            window.Show( );
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
        void ModifyPrefab( GameObject obj, GameObject rootObj, ModifyMode mode ) {
            string prefabFilrPath = null;
            if ( mode == ModifyMode.Apply ) {
                prefabFilrPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot( rootObj );
                Debug.Log( "Prefab FilrPath: " + prefabFilrPath );
                log.Add( "Apply Start: " + obj );
                log.Add( "Prefab FilrPath: " + prefabFilrPath );
            } else {
                log.Add( "Revert Start: " + obj );
            }
            if ( addGameObjects ) {
                log.Add( "- AddGameObjects" );
                var addedObjects = PrefabUtility.GetAddedGameObjects(rootObj);
                foreach ( var item in addedObjects ) {
                    if ( !IsRecursiveChild( obj.transform, item.instanceGameObject.transform ) ) {
                        continue;
                    }
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
                var addedComponents = PrefabUtility.GetAddedComponents(rootObj);
                foreach ( var item in addedComponents ) {
                    if ( !IsRecursiveChild( obj.transform, item.instanceComponent.transform ) ) {
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
                var removedComponents = PrefabUtility.GetRemovedComponents(rootObj);
                foreach ( var item in removedComponents ) {
                    if ( !IsRecursiveChild( obj.transform, item.containingInstanceGameObject.transform ) ) {
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
                var objectOverrides = PrefabUtility.GetObjectOverrides(rootObj);
                foreach ( var item in objectOverrides ) {
                    var targetOriginalObj = item.instanceObject as GameObject;
                    if ( targetOriginalObj == null ) {
                        targetOriginalObj = ( item.instanceObject as Component ).gameObject;
                    }
                    if ( !IsRecursiveChild( obj.transform, targetOriginalObj.transform ) ) {
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
        }
        private void OnGUI( ) {
            if ( EditorApplication.isPlaying ) {
                EditorGUILayout.HelpBox( "Playモード中は使用できません。\nCannot be used during Play mode.", MessageType.Warning );
                return;
            }
            var obj = Selection.activeGameObject;
            if ( obj == null || EditorUtility.IsPersistent( obj ) ) {
                EditorGUILayout.HelpBox( "シーン上にあるGameObjectを選択してください。\nSelect a GameObject on the scene.", MessageType.Warning );
                return;
            }
            var rootObj =  PrefabUtility.GetNearestPrefabInstanceRoot( obj );
            if ( rootObj == null ) {
                EditorGUILayout.HelpBox( "prefabに属しているオブジェクトを選択してください。\nSelect the objects belonging to the prefab.", MessageType.Warning );
                return;
            }
            using ( new EditorGUI.DisabledScope( true ) ) {
                EditorGUILayout.ObjectField( "Selected", obj, typeof( GameObject ), true );
                EditorGUILayout.ObjectField( "Root", rootObj, typeof( GameObject ), true );
            }
            addGameObjects = EditorGUILayout.Toggle( "Add GameObjects", addGameObjects );
            addComponents = EditorGUILayout.Toggle( "Add Components", addComponents );
            removeComponents = EditorGUILayout.Toggle( "Remove Components", addComponents );
            objectOverrides = EditorGUILayout.Toggle( "Object Overrides", objectOverrides );

            var tempColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color( 0.6f, 0.8f, 1 );
            if ( GUILayout.Button( "Apply Children", GUILayout.Height( 50 ) ) && EditorUtility.DisplayDialog( "Apply", "子オブジェクトをPrefabに Apply してもよろしいですか？\n\nAre you sure you want to APPLY children objects to Prefab?", "Apply", "No" ) ) {
                log.Clear( );
                ModifyPrefab( obj, rootObj, ModifyMode.Apply );
            }
            GUI.backgroundColor = tempColor;
            if ( GUILayout.Button( "Revert Children", GUILayout.Height( 50 ) ) && EditorUtility.DisplayDialog( "", "子オブジェクトをRevertしてもよろしいですか？\n\nAre you sure you want to Revert children objects to Prefab?", "Yes", "No" ) ) {
                log.Clear( );
                ModifyPrefab( obj, rootObj, ModifyMode.Revert );
            }
            scroll = EditorGUILayout.BeginScrollView( scroll );
            foreach ( var item in log ) {
                EditorGUILayout.LabelField( item );
            }
            EditorGUILayout.EndScrollView( );
        }
    }
}
#endif