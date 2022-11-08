using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class CreateHLSLFileEditor : Editor
{
    [MenuItem("Assets/Create/Shader/Create HLSL File",false,82)]
    static void CreateHLSLFile()
    {
        //当前项目的path 如：Assets/.../Scripts/Editor
        var curPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        //当前程序的path 如：E:/UnityProjectSpace/...../
        var path = Application.dataPath.Replace("Assets", "") ;
        var newFileName = "new hlsl file.hlsl";
        var newFilePath = curPath + "/" + newFileName;
        var fullPath = path + newFilePath;


        if (File.Exists(fullPath))
        {
            var newName = "new hlsl file" + Random.Range(0, 100) + ".hlsl";
            newFilePath = curPath + "/" + newFileName;
            fullPath = fullPath.Replace(newFileName, newName);
        }

        File.WriteAllText(fullPath,"",Encoding.UTF8);

        AssetDatabase.Refresh();

        var asset = AssetDatabase.LoadAssetAtPath(newFileName, typeof(Object));
        Selection.activeObject = asset;
    }
}
