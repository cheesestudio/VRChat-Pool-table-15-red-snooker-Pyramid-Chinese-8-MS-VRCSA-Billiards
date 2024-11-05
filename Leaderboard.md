#  密钥上传系统使用教程
### 目录
* #### 1.介绍和快速入门
* #### 2.插件依赖性
* #### 3.其他声明
---
## 1.介绍和快速入门
#### 1.1 该系统插件大纲

<table>
   <tr>
         <td>名称</td>
         <td>作用</td>
     	  <td>作用域</td>
   </tr>
	<tr>
        <td>上传插件(UNITY插件)</td>
        <td>上传密钥并手动绑定对于计分器的密钥 (可以不用手动绑定,我写了自动)</td>
        <td>仅限编辑器</td>
   </tr>
  	<tr>
        <td>上传时自动绑定脚本</td>
        <td>在上传时自动读取已经存储的KEY文件,并初始化计分器密钥</td>
        <td>仅限编辑器</td>
    </tr>
</table>


#### 1.2 上传KEY方法和重点
> 1.你需要在`VRC SDK` 中已经`上传过世界`,并且在 `Pipeline Manager` 中含有`WorldID`<br>

> 2.你需要确保 `Pipeline Manager` 中的 `World GUID 是你所需要上传的目标地图`,而不是什么测试图 `(尤其是和其他人使用 SCM 等工具 多人协同时)`<br>

> 3.满足以上两条可以在Unity`顶部菜单栏`找到 `MS-VRCSA/Upload Map Key` 选项,打开它,现在在你面前应该有一个`窗口`<br>

> 4.打开窗口后,`不必在意那两个URL`(晚点会去除掉,调试用的)点击上传分数并生成,并等待5-10秒,若出现`上传成功,请等待审核`,则代表KEY已`成功录入`,这个时候就该发个Issues催cheese通过审核(bushi<br>

> 5.如果出现`“请先完成首次上传”` 则代表你的地图`没有`在`SDK 里面上传过`,你需要先`上传地图在SDK里面`,或者需要检查`Pipeline Manager`是否为`空`,若为空则需要你在SDK中选择你自己的地图<br>

> 6.当出现上传完成后在 `Assets` 下会生成一个 `VRChatPoolMapKey.txt` 的文件,`请保管好它(备份一下)`,因为`地图上传一次后无法再次上传`,若其丢失,需要跑群里找管理员帮你重置地图KEY<br>

> 7.在上传地图到VRC前还需要`检查` VRChatPoolMapKey 中的 `GUID` 是否和你的`即将上传`的地图 GUID `一致`,是否使用到了错误的 KEY<br>

#### 1.3 快速查错

> ### 文字定义 
> 	Pipeline Manager 以下简称 PM
> 	VRChatPoolMapKey 以下简称 VPK

<table>	
   <tr>
         <td>返回消息</td>
         <td>代表含义</td>
     	  <td>如何快速解决</td>
   </tr>
	<tr>
        <td>上传成功,请等待审核</td>
        <td>已完成</td>
        <td>耐心等待,或发个Issues催cheese (bushi</td>
   </tr>
  	<tr>
        <td>请先完成首次上传</td>
        <td>PM 为空</td>
        <td>请检查地图在SDK中是否已经完成首次上传,PM中是否为空,若完成首次上传,但是PM为空,则需要您在SDK中手动选择地图</td>
   </tr>
   <tr>
        <td>上传失败,是否已上传过</td>
        <td>已经上传过了地图的 WorldKey</td>
        <td>若在 Assets 文件夹下能找到 VPK 那不用担心,这是正常的,若找不到或丢失,可以去群里让管理员帮你刷新KEY</td>
   </tr>
   <tr>
        <td>未知错误</td>
        <td>找到了 PM 但是 WorldGUID 无法获取到世界名称</td>
        <td>该错误含两种情况 1.您使用了错误的WorldGUID 2.当前网络环境不佳,无法链接VRC服务器</td>
   </tr>
</table>

##  2.插件依赖性

<table>
   <tr>
         <td>名称</td>
         <td>作用</td>
     	  <td>必要性</td>
   </tr>
	<tr>
        <td>积分榜系统</td>
        <td>上传排名到服务器并统计</td>
        <td>*必须的</td>
   </tr>
</table>

## 3.额外声明 
#### 1.该插件可能会记录您的 `WorldGUID` 和 `世界名称` 以便 `审核地图` 和 `分发密钥`

Copyright (c) 2024 WangQAQ
