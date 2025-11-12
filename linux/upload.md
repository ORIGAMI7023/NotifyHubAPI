 # 前端
  scp -r D:\Programing\C#\FileHostAPI\FileHost\bin\Release\net8.0\publish\* root@downf.cn:/var/www/filehost/

  # 后端
  scp -r D:\Programing\C#\FileHostAPI\FileHostAPI\bin\Release\net8.0\publish\* root@downf.cn:/var/www/filehostapi/

  2. 上传配置文件（直接覆盖）

  # nginx 配置（直接覆盖原文件）
  scp D:\Programing\C#\FileHostAPI\linux\downf.cn.conf root@downf.cn:/etc/nginx/conf.d/downf.cn.conf

  # 上传新的 systemd 服务配置
  scp D:\Programing\C#\FileHostAPI\linux\filehost.service root@downf.cn:/etc/systemd/system/filehost.service

  3. 配置应用（在服务器上执行）

  # 重载 nginx
  sudo nginx -t && sudo systemctl reload nginx


  # 重启服务】
  


  sudo systemctl daemon-reload
  sudo systemctl restart filehost
  sudo systemctl status filehost
  sudo systemctl restart filehostapi
  sudo systemctl status filehostapi
