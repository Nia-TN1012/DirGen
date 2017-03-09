using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml.Linq;

namespace DirGen {

	class Program {

		static void Main( string[] args ) {

			// 引数の数を判別します。
			if( args.Length >= 1 ) {

				// 第1引数が「-xml」の時、ディレクトリー構成とトークン情報の定義した雛形のXMLファイルを作成します。
				if( args[0] == "-xml" ) {
					if( args.Length >= 2 ) {
						CreateXML( args[1] );
					}
					else {
						Console.WriteLine( $@"'{args[0]}' オプションの後に、作成する定義ファイルの名前を指定してください。 

オプション :
  -xml [XMLファイル名] : ディレクトリー構成・トークン情報の定義ファイルを作成します。" );
					}
				}

				// 指定したファイルが存在する時
				else if( File.Exists( args[0] ) ) {
					RunDirGen( args[0] );
				}
				else {
					Console.WriteLine( $"エラー : 指定した定義ファイル '{args[0]}' が存在しないか、ファイルパスが無効です。" );
				}
			}
			else {

				var verInfo = FileVersionInfo.GetVersionInfo( Assembly.GetExecutingAssembly().Location );

				Console.WriteLine( $@"DirGen ( Directory Generator )
Version {verInfo.ProductVersion}
開発者 : Nia Tomonaka ( @nia_tn1012 )
(C) 2014-2017 Chronoir.net{Environment.NewLine}" );
				Console.WriteLine( @"使い方 : 
  ディレクトリー構成・トークン情報の定義ファイル1つを、実行ファイルにドラッグ＆ドロップします。
  定義ファイル（XML形式）の記述方法は、DirGenマニュアル.pdf を参照してください。

オプション :
  -xml [XMLファイル名] : ディレクトリー構成・トークン情報の定義ファイルの雛型を作成します。" );
			}

			Console.WriteLine( $"{Environment.NewLine}アプリを終了する時は、何かキーを押してください。" );
			Console.ReadKey();
		}

		/// <summary>
		///		ディレクトリー構成とトークン情報を定義したXMLファイルを読み込み、ディレクトリーを作成します。
		/// </summary>
		/// <param name="xmlPath">XMLファイルのパス</param>
		static void RunDirGen( string xmlPath ) {
			try {
				var dirGen = new DirectoryGenerator();
				// メッセージを受信したときの処理を指定します。
				dirGen.NotifyMessageRecieved +=
					( sender, e ) =>
						Console.WriteLine( e.Message );

				dirGen.Load( xmlPath );
				dirGen.GenerateDirectory();
			}
			catch( Exception e ) {
				Console.WriteLine( $"エラー : {e.Message}" );
			}
		}

		/// <summary>
		///		ディレクトリー構成とトークン情報の定義した雛形のXMLファイルを作成します。
		/// </summary>
		/// <param name="xmlPath">XMLファイルのパス</param>
		static void CreateXML( string xmlPath ) {

			Console.WriteLine( $"ディレクトリー構成・トークン情報の定義ファイルの雛型 '{xmlPath}' を作成しています。" );

			try {
				var targetPath = Path.GetDirectoryName( xmlPath );

				new XElement( "dirgen",
					new XComment( "'directory' ノードにディレクトリー構成情報を記述します。" ),
					new XComment( "'name' 属性には、出力先パスに作成するルートフォルダーの名前を入力します。省略した場合、ルートフォルダーを作成せず、子ノード内で定義を元にディレクトリーを直接作成します。" ),
					new XComment( "'target_path' 属性には、出力先のパスを入力します。省略した場合、アプリと同じパスとなります。" ),
					new XElement( "directory",
						new XAttribute( "name", Path.GetFileNameWithoutExtension( xmlPath ) ),
						string.IsNullOrEmpty( targetPath ) ? null : new XAttribute( "target_path", targetPath ),
						new XComment( "'folder' ノードは、'name' 属性で指定したフォルダーを作成します。" ),
						new XElement( "folder",
							new XAttribute( "name", "Sample1" )
						),
						new XElement( "folder",
							new XAttribute( "name", "Sample2" ),
							new XElement( "folder",
								new XAttribute( "name", "Sub" )
							),
							new XComment( "'name' 属性の値の先頭に '$' を付けると、トークンとして認識し、'token' ノードで指定したトークンの値を使用します。" ),
							new XElement( "folder",
								new XAttribute( "name", "$Token" ),
								new XComment( "'file' ノードは、'name' 属性で指定したファイルを出力先にコピーします。" ),
								new XElement( "file",
									new XAttribute( "name", "$File" )
								)
							)
						)
					),
					new XComment( "'tokens' ノードにトークン情報を記述します。" ),
					new XComment( "トークンを利用すると、同じサブディレクトリー構成を持つフォルダー名をまとめることができます。" ),
					new XElement( "tokens",
						new XElement( "token",
							new XAttribute( "key", "Token" ),
							new XElement( "token_value", new XAttribute( "value", "Apple" ) ),
							new XElement( "token_value", new XAttribute( "value", "Banana" ) ),
							new XElement( "token_value", new XAttribute( "value", "Orange" ) )
						),
						new XElement( "token",
							new XAttribute( "key", "File" ),
							new XElement( "token_value", new XAttribute( "value", xmlPath ) )
						)
					)
				).Save( xmlPath );

				Console.WriteLine( $"XMLファイルの作成が完了しました。" );
			}
			catch( Exception e ) {
				Console.WriteLine( $"エラー : {e.Message}" );
			}
		}
	}
}
