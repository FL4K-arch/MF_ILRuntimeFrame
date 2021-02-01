using ILRuntime.Runtime.Enviorment;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class ILManager : MonoBehaviour
{
    
    public string versionName = "dllVersion.txt";
    public string server = "";//Զ�̷�������ַ ����ģʽ�� �ͽ�������Ϊ����ɳ��Ŀ¼�Ϳ�����
    public string local = "";//���ػ���Ŀ¼
    public bool isDebug = false;
    public string hotfixdllName = "hotfix.dll";
    public string hotfixpdbName = "hotfix.pdb";

    DllVersion localDllVersion;
    DllVersion serverDllVersion;

    List<string> waitDownloadTasks = new List<string>();
    Dictionary<string, bool> downloaded = new Dictionary<string, bool>();

    public string localVersion;//���صİ汾��Ϣ

    /// <summary> ����dll������ </summary>
    public void UpdateConfig() {
    
    
    }


    /// <summary> ��ʼ�� </summary>
    public void Init()
    {

        local = Path.Combine(Application.persistentDataPath, "Dll");
        server = local;//����
        //δ��������dll��Ŀ¼ �򴴽�һ��
        if (!Directory.Exists(local))
        {
            Directory.CreateDirectory(local);
        }

        localVersion = Path.Combine(local, versionName);
        //�Ѿ����������ļ���
        if (File.Exists(localVersion))
        {
            this.localDllVersion = LitJson.JsonMapper.ToObject<DllVersion>(File.ReadAllText(localVersion));
        }
    }

    /// <summary> ��ȡԶ��dll�İ汾 </summary>
    public IEnumerator GetServerDllVersion() {

        while (serverDllVersion == null)
        {
            using (UnityWebRequest webRequest = UnityWebRequest.Get(Path.Combine(server, versionName)))
            {
                yield return webRequest.SendWebRequest();
                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("����ʧ��");

                    //���Ե���֮�����ʾ Ȼ��ͨ�������﷨�������� 
                    //bool reGet = false;
                    //yield return new WaitUntil((() => reGet));

                    yield return new WaitForSeconds(1);
                }
                else
                {
                    Debug.Log($"Get Content:{webRequest.downloadHandler.text}");
                    serverDllVersion = LitJson.JsonMapper.ToObject<DllVersion>(webRequest.downloadHandler.text);
                }
            }
        }
    }

  
    /// <summary> ���бȽ� </summary>
    public void CheckIsUpdate() {
        if (serverDllVersion == null)
        {
            Debug.LogError("�������󵽷��������ļ�����");
            return;
        }

        for (int i = 0; i < serverDllVersion.dllFile.Count; i++)
        {
            var item = serverDllVersion.dllFile.ElementAt(i);
            if (localDllVersion == null)
            {
                waitDownloadTasks.Add(item.Key);
            }
            else
            {
                //if (item.Value.md5!=MD5Helper.FileMD5( Path.Combine(dllSaveDirectory, item.Key)))
                if (item.Value.md5 != localDllVersion.dllFile[item.Key].md5)
                {
                    waitDownloadTasks.Add(item.Key);
                }
                else
                {
                    Debug.Log($"{item.Key} md5 һ��!");
                }
            }
        }
    }


    /// <summary> ��ʼ�������� </summary>
    public IEnumerator DownloadTasks() {
        if (waitDownloadTasks.Count != 0)
        {
            for (int i = 0; i < waitDownloadTasks.Count; i++)
            {
                downloaded.Add(waitDownloadTasks[i],false);
                StartCoroutine(CreateDowloadTask(waitDownloadTasks[i]));
            }
            yield return new WaitUntil(() => downloaded.Values.ToList().TrueForAll(o => { return o; }));
            downloaded.Clear();
            //���ǵý��汾�ļ���������
            File.WriteAllText(localVersion,LitJson.JsonMapper.ToJson(serverDllVersion));
        }
    }

    /// <summary> ������������</summary> 
    public IEnumerator CreateDowloadTask(string file)
    {
        while (true)
        {
            using (UnityWebRequest webRequest = UnityWebRequest.Get(Path.Combine(server, file)))
            {
                yield return webRequest.SendWebRequest();
                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"{file}����ʧ��,�ȴ�1���������� ���⿨��!!!  {webRequest.result}");
                    yield return new WaitForSeconds(1);
                }
                else
                {
                    //д�뵽����ȥ 
                    File.WriteAllBytes(Path.Combine(local, file), webRequest.downloadHandler.data);
                    downloaded[file] = true;
                    break;
                }
            }
        }

    }

    ILRuntime.Runtime.Enviorment.AppDomain appdomain;
    System.IO.MemoryStream fs;
    System.IO.MemoryStream p;
    /// <summary> ����HotfixDll</summary> 
    public IEnumerator LoadHotfixDll() {
        appdomain = new ILRuntime.Runtime.Enviorment.AppDomain();
       
        //PDB�ļ��ǵ������ݿ⣬����Ҫ����־����ʾ������кţ�������ṩPDB�ļ����������ڻ��������ڴ棬��ʽ����ʱ�뽫PDBȥ��������LoadAssembly��ʱ��pdb��null����
        using (UnityWebRequest dllRequest = UnityWebRequest.Get(Path.Combine(local, hotfixdllName)))
        {
            yield return dllRequest.SendWebRequest();
            //����������״̬
            if (dllRequest.result!= UnityWebRequest.Result.Success)
            {
                Debug.LogError("δ���ص��ȸ�dll");
            }
            else
            {
                fs = new MemoryStream(dllRequest.downloadHandler.data);
                if (isDebug)
                {
                    using (UnityWebRequest pdbRequest = UnityWebRequest.Get(Path.Combine(local, hotfixpdbName))) {
                        yield return pdbRequest.SendWebRequest();
                        if (pdbRequest.result!= UnityWebRequest.Result.Success)
                        {
                            Debug.LogError("δ���ص�pdb�������ݿ�");
                        }
                        p= new MemoryStream(pdbRequest.downloadHandler.data);
                    }
                }
            }

            try
            {
                if (isDebug)
                {
                    appdomain.LoadAssembly(fs,  p , new ILRuntime.Mono.Cecil.Pdb.PdbReaderProvider());
                }
                else
                {
                    appdomain.LoadAssembly(fs, null, null);
                }
               
            }
            catch(Exception e)
            {
                Debug.LogError($"appdomain LoadAssembly Error:{e.Message}");
            }

            InitializeILRuntime();
            OnHotFixLoaded();
        }


    }

    /// <summary> ��ʼ��ILRuntime </summary>
    public void InitializeILRuntime()
    {
#if DEBUG && (UNITY_EDITOR || UNITY_ANDROID || UNITY_IPHONE)
        //����Unity��Profiler�ӿ�ֻ���������߳�ʹ�ã�Ϊ�˱�����쳣����Ҫ����ILRuntime���̵߳��߳�ID������ȷ���������к�ʱ�����Profiler
        appdomain.UnityMainThreadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
#endif
        //������һЩILRuntime��ע��

        ILAdaptor.RegisterAdaptor(appdomain);//����̳���������ע��
        ILDelegate.RegisterDelegate(appdomain);//ί��������

        //LitJson�ض���
        LitJson.JsonMapper.RegisterILRuntimeCLRRedirection(appdomain);

        //�����ֻ�������˰󶨴���֮�� ���ܹ����õ�
        //ILRuntime.Runtime.Generated.CLRBindings.Initialize(appdomain);
    }

    /// <summary> �����ȸ��������� </summary>
    void OnHotFixLoaded()
    {
        appdomain.Invoke("Hotfix.HotfixCodeManager", "Main", null, null);
    }


    private void OnDestroy()
    {
        if (fs != null)
            fs.Close();
        if (p != null)
            p.Close();
        fs = null;
        p = null;
    }
}
