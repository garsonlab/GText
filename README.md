# GText
Based on UGUI to support sprite and hyper link on Text component.

extend from [**EmojiText**](https://github.com/zouchunyi/EmojiText)

Unity 图文混排和超链接解决方案

Unity version > 5

### How to use?
    1)
        Put all sprites in Assets.
        Multi-frame sprite name format : Name_Index.png , Single frame emoji format: Name.png
    2)
        Right click on choosing sprites and select Assets->Build Emoji to excute
    3)
        Select folder to save. We will create "Emoji Teture" , "Emoji Data" , a material use "UI/EmojiFont" and "Emoji.txt"
    4)
        Create a Text component in UI and selct "UI/Text->GText",
        Then you can enjoy it.
    5)
        You can modify "GText.cs" :
            void LoadEmojiData()
        to yourself load way in game.

One Gtext only one DC and can batch.

![](https://github.com/garsonlab/GText/raw/master/Screenshot.png)