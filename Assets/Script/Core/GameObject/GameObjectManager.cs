﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class GameObjectManager
{
    static Vector3 s_OutOfRange = new Vector3(9000, 9000, 9000);

    private static Transform s_poolParent;
    public static Transform PoolParent
    {
        get
        {
            if (s_poolParent == null)
            {
                GameObject instancePool = new GameObject("ObjectPool");
                s_poolParent = instancePool.transform;
                if (Application.isPlaying)
                    GameObject.DontDestroyOnLoad(s_poolParent);
            }

            return s_poolParent;
        }
    }

    #region 旧版本对象池
    private static Dictionary<string, List<GameObject>> createPools = new Dictionary<string, List<GameObject>>();
    private static Dictionary<string, List<GameObject>> recyclePools = new Dictionary<string, List<GameObject>>();

    public static Dictionary<string, List<GameObject>> GetCreatePool()
    {
        return createPools;
    }
    public static Dictionary<string, List<GameObject>> GetRecyclePool()
    {
        return recyclePools;
    }
    /// <summary>
    /// 加载一个对象并把它实例化
    /// </summary>
    /// <param name="gameObjectName">对象名</param>
    /// <param name="parent">对象的父节点,可空</param>
    /// <returns></returns>
    private static GameObject NewGameObject(string gameObjectName, GameObject parent = null)
    {
        GameObject goTmp = AssetsPoolManager.Load<GameObject>(gameObjectName);

        if (goTmp == null)
        {
            throw new Exception("CreateGameObject error dont find :" + gameObjectName);
        }

        return ObjectInstantiate(goTmp, parent);
    }

    private static GameObject ObjectInstantiate(GameObject prefab, GameObject parent = null)
    {
        if (prefab == null)
        {
            throw new Exception("CreateGameObject error : l_prefab  is null");
        }
        Transform transform = parent == null ? null : parent.transform;
        GameObject instanceTmp = GameObject.Instantiate(prefab, transform);
        instanceTmp.name = prefab.name;
        return instanceTmp;
    }


    public static bool IsExist(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            Debug.LogError("GameObjectManager objectName is null!");
            return false;
        }

        if (recyclePools.ContainsKey(objectName) && recyclePools[objectName].Count > 0)
        {
            return true;
        }

        return false;
    }

    //判断是否在对象池中
    public static bool IsExist(GameObject go)
    {
        if (recyclePools.ContainsKey(go.name) && recyclePools[go.name].Count > 0)
        {
            return recyclePools[go.name].Contains(go);
        }
        else
        {
            return false;
        }
    }

   
    public static GameObject CreateGameObject(string name, GameObject parent = null, bool isSetActive = true)
    {
        return GetNewObject(true, name, null, parent, isSetActive);
    }

    public static GameObject CreateGameObject(GameObject prefab, GameObject parent = null, bool isSetActive = true)
    {
        return GetNewObject(true, null, prefab, parent, isSetActive);
    }
    /// <summary>
    /// 从对象池取出一个对象，如果没有，则直接创建它
    /// </summary>
    /// <param name="name">对象名</param>
    /// <param name="parent">要创建到的父节点</param>
    /// <returns>返回这个对象</returns>
    public static GameObject CreateGameObjectByPool(string name, GameObject parent = null, bool isSetActive = true)
    {
        return GetNewObject(false, name, null, parent, isSetActive);
    }

    public static GameObject CreateGameObjectByPool(GameObject prefab, GameObject parent = null, bool isSetActive = true)
    {
        return GetNewObject(false, null, prefab, parent, isSetActive);
    }
    private static GameObject GetNewObject(bool isAlwaysNew, string objName, GameObject prefab, GameObject parent = null, bool isSetActive = true)
    {
        GameObject go = null;
        string name = objName;
        if (string.IsNullOrEmpty(name))
        {
            name = prefab.name;
        }

        if (!isAlwaysNew && IsExist(name))
        {
            go = recyclePools[name][0];
            recyclePools[name].RemoveAt(0);

            AssetsPoolManager.MarkeFlag(name, typeof(GameObject));
        }
        else
        {
            if (prefab == null && !string.IsNullOrEmpty(objName))
            {
                go = NewGameObject(name, parent);
                if (createPools.ContainsKey(name))
                {
                    createPools[name].Add(go);
                }
                else
                {
                    createPools.Add(name, new List<GameObject>() { go });
                }
            }
            else if (prefab != null && string.IsNullOrEmpty(objName))
            {
                go = ObjectInstantiate(prefab, parent);
            }
        }

        PoolObject po = go.GetComponent<PoolObject>();
        if (po)
        {
            try
            {
                po.OnFetch();
            }
            catch(Exception e)
            {
                Debug.LogError("GetNewObject Error: " + e.ToString());
            }
        }

        if (isSetActive)
            go.SetActive(true);

        if (parent == null)
        {
            go.transform.SetParent(null);
        }
        else
        {
            go.transform.SetParent(parent.transform);
        }
        return go;
    }

    /// <summary>
    /// 将一个对象放入对象池
    /// </summary>
    /// <param name="go"></param>
    /// <param name="isSetInactive">是否将放入的物体设为不激活状态（obj.SetActive(false)）</param>
    public static void DestroyGameObjectByPool(GameObject go, bool isSetInactive = true)
    {
        if (go == null)
            return;

        string key = go.name.Replace("(Clone)", "");
        if (recyclePools.ContainsKey(key) == false)
        {
            recyclePools.Add(key, new List<GameObject>());
        }

        if (recyclePools[key].Contains(go))
        {
            Debug.LogError("DestroyGameObjectByPool:-> Repeat Destroy GameObject !" + go);
            return;
        }

        recyclePools[key].Add(go);

        if (isSetInactive)
            go.SetActive(false);
        else
        {
            go.transform.position = s_OutOfRange;
        }

        go.name = key;
        go.transform.SetParent(PoolParent);
        PoolObject po = go.GetComponent<PoolObject>();
        if (po)
        {
            po.OnRecycle();
        }


        if (createPools.ContainsKey(key) && createPools[key].Contains(go))
        {
            createPools[key].Remove(go);
        }

    }
    /// <summary>
    /// 立即摧毁克隆体
    /// </summary>
    /// <param name="go"></param>
    public static void DestroyGameObject(GameObject go)
    {
        if (go == null)
            return;

        string key = go.name.Replace("(Clone)", "");

        PoolObject po = go.GetComponent<PoolObject>();
        if (po)
        {
            po.OnObjectDestroy();
        }

        if (createPools.ContainsKey(key) && createPools[key].Contains(go))
        {
            createPools[key].Remove(go);
        }
        UnityEngine.Object.Destroy(go);
    }

    public static void DestroyGameObjectByPool(GameObject go, float time)
    {
        Timer.DelayCallBack(time, (object[] obj) =>
        {
            if (go != null)//应对调用过CleanPool()
                DestroyGameObjectByPool(go);
        });
    }

    private static List<string> removeObjList = new List<string>();
    /// <summary>
    /// 清空对象池
    /// </summary>
    public static void CleanPool()
    {
        //Debug.LogWarning("清空对象池");
        removeObjList.Clear();

        foreach(string name in createPools.Keys)
        {
            //if (recyclePools.ContainsKey(name))
            //{
            //    List<GameObject> l_objList = recyclePools[name];

            //    for (int i = 0; i < l_objList.Count; i++)
            //    {
            //        GameObject go = l_objList[i];

            //        PoolObject po = go.GetComponent<PoolObject>();
            //        if (po)
            //        {
            //            po.OnObjectDestroy();
            //        }

            //        GameObject.Destroy(go);
            //    }
            //    l_objList.Clear();
            //    recyclePools.Remove(name);
            //}

            if(createPools[name].Count == 0)
            {
                removeObjList.Add(name);
                AssetsPoolManager.DestroyByPool(name);
            }
        }
        
        foreach (var item in removeObjList)
        {
            createPools.Remove(item);
        }

        foreach (var name in recyclePools.Keys)
        {
                List<GameObject> l_objList = recyclePools[name];

                for (int i = 0; i < l_objList.Count; i++)
                {
                    GameObject go = l_objList[i];

                    PoolObject po = go.GetComponent<PoolObject>();
                    if (po)
                    {
                        po.OnObjectDestroy();
                    }

                    GameObject.Destroy(go);
                }
                l_objList.Clear();
            
        }
        recyclePools.Clear();

    }

    /// <summary>
    /// 清除掉某一个对象的所有对象池缓存
    /// </summary>
    public static void CleanPoolByName(string name)
    {
        Debug.Log("CleanPool :" + name);
        if (recyclePools.ContainsKey(name))
        {
            List<GameObject> l_objList = recyclePools[name];

            for (int i = 0; i < l_objList.Count; i++)
            {
                GameObject go = l_objList[i];

                PoolObject po = go.GetComponent<PoolObject>();
                if (po)
                {
                    po.OnObjectDestroy();
                }

                GameObject.Destroy(go);
            }
            l_objList.Clear();
            recyclePools.Remove(name);
        }

        if (createPools[name].Count == 0)
        {
            createPools.Remove(name);
            AssetsPoolManager.DestroyByPool(name);
        }
    }

    #endregion

    #region 旧版本对象池 异步方法

    //public static void CreateGameObjectByPoolAsync(string name, CallBack<GameObject> callback, GameObject parent = null, bool isSetActive = true)
    //{
    //    AssetsPoolManager.LoadAsync(name,null, (status, res) =>
    //    {
    //        if(status.isDone)
    //        {
    //            try
    //            {
    //                callback(CreateGameObjectByPool(name, parent, isSetActive));
    //            }
    //            catch (Exception e)
    //            {
    //                Debug.LogError("CreateGameObjectByPoolAsync Exception: " + e.ToString());
    //            }
    //        }
    //    });
    //}

    #endregion

    //#region 新版本对象池

    //static Dictionary<string, List<PoolObject>> s_objectPool_new = new Dictionary<string, List<PoolObject>>();

    ///// <summary>
    ///// 加载一个对象并把它实例化
    ///// </summary>
    ///// <param name="gameObjectName">对象名</param>
    ///// <param name="parent">对象的父节点,可空</param>
    ///// <returns></returns>
    //static PoolObject CreatePoolObject(string gameObjectName, GameObject parent = null)
    //{
    //    GameObject go = ResourceManager.Load<GameObject>(gameObjectName);

    //    if (go == null)
    //    {
    //        throw new Exception("CreatPoolObject error dont find : ->" + gameObjectName + "<-");
    //    }

    //    GameObject instanceTmp = Instantiate(go);
    //    instanceTmp.name = go.name;

    //    PoolObject po = instanceTmp.GetComponent<PoolObject>();

    //    if (po == null)
    //    {
    //        throw new Exception("CreatPoolObject error : ->" + gameObjectName + "<- not is PoolObject !");
    //    }

    //    po.OnCreate();

    //    if (parent != null)
    //    {
    //        instanceTmp.transform.SetParent(parent.transform);
    //    }

    //    instanceTmp.SetActive(true);

    //    return po;
    //}

    ///// <summary>
    ///// 把一个对象放入对象池
    ///// </summary>
    ///// <param name="gameObjectName"></param>
    //public static void PutPoolObject(string gameObjectName)
    //{
    //    DestroyPoolObject(CreatePoolObject(gameObjectName));
    //}

    ///// <summary>
    ///// 预存入对象池
    ///// </summary>
    ///// <param name="name"></param>
    //public static void PutPoolGameOject(string name)
    //{
    //    DestroyGameObjectByPool(CreateGameObjectByPool(name));
    //}

    //public static bool IsExist_New(string objectName)
    //{
    //    if (objectName == null)
    //    {
    //        Debug.LogError("IsExist_New error : objectName is null!");
    //        return false;
    //    }

    //    if (s_objectPool_new.ContainsKey(objectName) && s_objectPool_new[objectName].Count > 0)
    //    {
    //        return true;
    //    }

    //    return false;
    //}

    ///// <summary>
    ///// 从对象池取出一个对象，如果没有，则直接创建它
    ///// </summary>
    ///// <param name="name">对象名</param>
    ///// <param name="parent">要创建到的父节点</param>
    ///// <returns>返回这个对象</returns>
    //public static PoolObject GetPoolObject(string name, GameObject parent = null)
    //{
    //    PoolObject po;
    //    if (IsExist_New(name))
    //    {
    //        po = s_objectPool_new[name][0];
    //        s_objectPool_new[name].RemoveAt(0);
    //        if (po && po.SetActive)
    //            po.gameObject.SetActive(true);

    //        if (parent == null)
    //        {
    //            po.transform.SetParent(null);
    //        }
    //        else
    //        {
    //            po.transform.SetParent(parent.transform);
    //        }
    //    }
    //    else
    //    {
    //        po = CreatePoolObject(name, parent);
    //    }

    //    po.OnFetch();

    //    return po;
    //}

    ///// <summary>
    ///// 将一个对象放入对象池
    ///// </summary>
    ///// <param name="obj">目标对象</param>
    //public static void DestroyPoolObject(PoolObject obj)
    //{
    //    string key = obj.name.Replace("(Clone)", "");

    //    if (s_objectPool_new.ContainsKey(key) == false)
    //    {
    //        s_objectPool_new.Add(key, new List<PoolObject>());
    //    }

    //    if (s_objectPool_new[key].Contains(obj))
    //    {
    //        throw new Exception("DestroyPoolObject:-> Repeat Destroy GameObject !" + obj);
    //    }

    //    s_objectPool_new[key].Add(obj);

    //    if (obj.SetActive)
    //        obj.gameObject.SetActive(false);
    //    else
    //        obj.transform.position = s_OutOfRange;

    //    obj.OnRecycle();

    //    obj.name = key;
    //    obj.transform.SetParent(PoolParent);
    //}

    //public static void DestroyPoolObject(PoolObject go, float time)
    //{
    //    Timer.DelayCallBack(time, (object[] obj) =>
    //    {
    //        DestroyPoolObject(go);
    //    });
    //}

    ///// <summary>
    ///// 清空对象池
    ///// </summary>
    //public static void CleanPool_New()
    //{
    //    foreach (string name in s_objectPool_new.Keys)
    //    {
    //        if (s_objectPool_new.ContainsKey(name))
    //        {
    //            List<PoolObject> objList = s_objectPool_new[name];

    //            for (int i = 0; i < objList.Count; i++)
    //            {
    //                try
    //                {
    //                    objList[i].OnObjectDestroy();
    //                }
    //                catch (Exception e)
    //                {
    //                    Debug.Log(e.ToString());
    //                }

    //                Destroy(objList[i].gameObject);
    //            }

    //            objList.Clear();
    //        }
    //    }

    //    s_objectPool_new.Clear();
    //}

    ///// <summary>
    ///// 清除掉某一个对象的所有对象池缓存
    ///// </summary>
    //public static void CleanPoolByName_New(string name)
    //{
    //    if (s_objectPool_new.ContainsKey(name))
    //    {
    //        List<PoolObject> objList = s_objectPool_new[name];

    //        for (int i = 0; i < objList.Count; i++)
    //        {
    //            try
    //            {
    //                objList[i].OnObjectDestroy();
    //            }
    //            catch(Exception e)
    //            {
    //                Debug.Log(e.ToString());
    //            }

    //            Destroy(objList[i].gameObject);
    //        }

    //        objList.Clear();
    //        s_objectPool_new.Remove(name);
    //    }
    //}

    //#endregion

    //#region 新版本对象池 异步方法

    //public static void CreatePoolObjectAsync(string name, CallBack<PoolObject> callback, GameObject parent = null)
    //{
    //    ResourceManager.LoadAsync(name, (status,res) =>
    //    {
    //        try
    //        {
    //            callback(CreatePoolObject(name, parent));
    //        }
    //        catch(Exception e)
    //        {
    //            Debug.LogError("CreatePoolObjectAsync Exception: " + e.ToString());
    //        }
    //    });
    //}

    //#endregion
}
