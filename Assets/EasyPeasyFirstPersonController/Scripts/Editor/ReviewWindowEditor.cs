using EasyPeasyFirstPersonController;
using UnityEditor;
using UnityEngine;

namespace EasyPeasyFirstPersonController.EditorScripts
{
    [CustomEditor(typeof(FirstPersonController))]
    [InitializeOnLoad]
    public class ReviewWindowEditor : Editor
    {
        private static string assetStoreUrl = "https://assetstore.unity.com/packages/slug/317073"; 

        static ReviewWindowEditor()
        {
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += DrawReviewIcon;
        }

        private static void DrawReviewIcon(EntityId entityId, Rect selectionRect)
        {
            try
            {
                Object obj = EditorUtility.EntityIdToObject(entityId);
                if (obj == null) return;
                GameObject go = obj as GameObject;
                if (go == null) return;

                if (go.GetComponent<FirstPersonController>() != null)
                {
                    Rect iconRect = new Rect(selectionRect.xMax - 20, selectionRect.y, 16, 16);
                    
                    GUIContent iconContent = EditorGUIUtility.IconContent("Favorite");
                    
                    if (iconContent == null || iconContent.image == null)
                        iconContent = EditorGUIUtility.IconContent("d_Favorite");

                    GUIContent finalContent = (iconContent != null && iconContent.image != null) 
                        ? new GUIContent(iconContent.image, "Rate this asset on Asset Store") 
                        : new GUIContent("★", "Rate this asset on Asset Store");

                    if (GUI.Button(iconRect, finalContent, GUIStyle.none))
                    {
                        Application.OpenURL(assetStoreUrl);
                    }
                }
            }
            catch
            {
                // Ignore transient hierarchy repaint exceptions during git/scene reloads
            }
        }
    }
}