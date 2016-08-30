# AsyncEchoServer

## 概要

AsyncEchoServerはC#で開発された非同期エコーサーバとなります。
本サーバーでは非同期通信を採用しているため、多数（4000以上）のクライアントと同時接続を行うことができます。

## ビルド方法
「build.bat」を実行します。本プロジェクトのビルドにはVisual Studio 2015が必要となります。

    build.bat

## インストール方法
コマンドプロンプト（管理者）を起動して、「install.bat」を実行します。

    install.bat

## アンインストール方法
コマンドプロンプト（管理者）を起動して、「uninstall.bat」を実行します。

    uninstall.bat

## 実行方法

タスクマネージャ等から AsyncEchoServer サービスを開始します。

次に AsyncEchoServer\bin\Release\EchoClient.exe を実行し下記のように出力されることを確認します。

    Hello.

## ライセンス
 MIT License