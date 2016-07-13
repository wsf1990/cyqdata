using CYQ.Data.Cache;
using System.Configuration;
using System;
using CYQ.Data.Table;

namespace CYQ.Data.Aop
{
    /// <summary>
    /// �ڲ�Ԥ��ʵ��CacheAop
    /// </summary>
    internal class InterAop
    {
        private CacheManage _Cache = CacheManage.LocalInstance;//Cache����
       // private AutoCache cacheAop = new AutoCache();
        private static readonly object lockObj = new object();
        private bool isHasCache = false;
        private bool isUseAop = true;
        internal bool IsCustomAop
        {
            get
            {
                return isUseAop && (AppConfig.Cache.IsAutoCache || outerAop != null);
            }
            set
            {
                isUseAop = value;
            }
        }
        internal bool IsTxtDataBase
        {
            get
            {
                return Para.DalType == DalType.Txt || Para.DalType == DalType.Xml;
            }
        }
        private AopInfo _AopInfo;
        /// <summary>
        /// Aop����
        /// </summary>
        public AopInfo Para
        {
            get
            {
                if (_AopInfo == null)
                {
                    _AopInfo = new AopInfo();
                }
                return _AopInfo;
            }
        }

        private IAop outerAop;
        public InterAop()
        {
            outerAop = GetFromConfig();
        }
        #region IAop ��Ա

        public AopResult Begin(AopEnum action)
        {
            AopResult ar = AopResult.Continue;
            if (outerAop != null)
            {
                ar = outerAop.Begin(action, Para);
                if (ar == AopResult.Return)
                {
                    return ar;
                }
            }
            if (AppConfig.Cache.IsAutoCache && !IsTxtDataBase) // ֻҪ����ֱ�ӷ���
            {
                isHasCache = AutoCache.GetCache(action, Para); //�ҿ���û��Cache
            }
            if (isHasCache)  //�ҵ�Cache
            {
                if (outerAop == null || ar == AopResult.Default)//��ִ��End
                {
                    return AopResult.Return;
                }
                return AopResult.Break;//�ⲿAop˵������Ҫִ��End
            }
            else // û��Cache��Ĭ�Ϸ���
            {
                return ar;
            }
        }

        public void End(AopEnum action)
        {
            if (outerAop != null)
            {
                outerAop.End(action, Para);
            }
            if (!isHasCache && !IsTxtDataBase)
            {
                AutoCache.SetCache(action, Para); //�ҿ���û��Cache
            }
        }

        public void OnError(string msg)
        {
            if (outerAop != null)
            {
                outerAop.OnError(msg);
            }
        }


        //public IAop Clone()
        //{
        //    return new InterAop();
        //}
        //public void OnLoad()
        //{

        //}
        #endregion
        static bool _CallOnLoad = false;
        private IAop GetFromConfig()
        {

            IAop aop = null;

            string aopApp = AppConfig.Aop;
            if (!string.IsNullOrEmpty(aopApp))
            {
                if (_Cache.Contains("Aop_Instance"))
                {
                    aop = _Cache.Get("Aop_Instance") as IAop;
                }
                else
                {
                    #region AOP����

                    string[] aopItem = aopApp.Split(',');
                    if (aopItem.Length == 2)//��������,����(dll)����
                    {
                        try
                        {
                            System.Reflection.Assembly ass = System.Reflection.Assembly.Load(aopItem[1]);
                            if (ass != null)
                            {
                                object instance = ass.CreateInstance(aopItem[0]);
                                if (instance != null)
                                {
                                    _Cache.Add("Aop_Instance", instance, AppConst.RunFolderPath + aopItem[1].Replace(".dll", "") + ".dll", 1440);
                                    aop = instance as IAop;
                                    if (!_CallOnLoad)
                                    {
                                        lock (lockObj)
                                        {
                                            if (!_CallOnLoad)
                                            {
                                                _CallOnLoad = true;
                                                aop.OnLoad();
                                            }
                                        }
                                    }
                                    return aop;
                                }
                            }
                        }
                        catch (Exception err)
                        {
                            string errMsg = err.Message + "--Web.config need add a config item,for example:<add key=\"Aop\" value=\"Web.Aop.AopAction,Aop\" />(value format:namespace.Classname,Assembly name) ";
                            Error.Throw(errMsg);
                        }
                    }
                    #endregion
                }
            }
            if (aop != null)
            {
                return aop.Clone();
            }
            return null;
        }

        #region �ڲ�����
        //public static InterAop Instance
        //{
        //    get
        //    {
        //        return Shell.instance;
        //    }
        //}

        //class Shell
        //{
        //    internal static readonly InterAop instance = new InterAop();
        //}
        #endregion
    }
}
