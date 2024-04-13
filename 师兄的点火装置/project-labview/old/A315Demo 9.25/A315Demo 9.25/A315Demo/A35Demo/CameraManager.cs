using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A35Demo
{
    public class CameraManager
    {
        private static CameraManager instance = null;
        private FlirCamera camera = null;

        private CameraManager()
        {
            // 初始化相机连接
            camera = new FlirCamera();
        }

        // 获取相机实例
        public static CameraManager GetInstance()
        {
            if (instance == null)
            {
                instance = new CameraManager();
            }
            return instance;
        }

        // 获取相机对象
        public FlirCamera GetCamera()
        {
            return camera;
        }
    }

}
