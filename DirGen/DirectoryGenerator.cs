using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace DirGen {

	/// <summary>
	///		メッセージを格納するイベント引数を提供します。
	/// </summary>
	public class MessageEventArgs : EventArgs {
		/// <summary>
		///		メッセージを取得します。
		/// </summary>
		public string Message { get; private set; }

		/// <summary>
		///		メッセージから<see cref="MessageEventArgs"/>クラスの新しいインスタンスを生成します。
		/// </summary>
		/// <param name="mes">メッセージ</param>
		public MessageEventArgs( string mes ) {
			Message = mes;
		}
	}

	/// <summary>
	///		ディレクトリー構造の定義ファイルを読み込み、ディレクトリーを作成するジェネレータークラスを提供します。
	/// </summary>
	public class DirectoryGenerator {

		/// <summary>
		///		出力先のルートパスを表します。
		/// </summary>
		private string targetRootPath;

		/// <summary>
		///		出力先のパスに作成するルートフォルダー名を表します。
		/// </summary>
		private string targetRootFolderName;

		/// <summary>
		///		ディレクトリー構造を表す、XMLツリーを表します。
		/// </summary>
		private XElement directoryTree;

		/// <summary>
		///		トークンなどの展開後のディレクトリー構造を表す、XMLツリーを表します。
		/// </summary>
		private XElement extractedDirectoryTree;

		/// <summary>
		///		トークンの辞書リストを表します。
		/// </summary>
		private Dictionary<string, string[]> tokenListMap;

		/// <summary>
		///		アプリのパスを表します。
		/// </summary>
		private static readonly string appPath = Directory.GetCurrentDirectory();

		/// <summary>
		///		定義ファイルが入っているパスを表します。
		/// </summary>
		private string definitionFilePath;

		/// <summary>
		///		ファイル名で利用できない文字を認識するための正規表現を表します。
		/// </summary>
		private static readonly Regex fileNameInvaildRecongnizer = new Regex( $"[{Regex.Escape( "\\/:*?\"<>|" )}]" );

		/// <summary>
		///		先頭に「$」が付いた名前をトークンとして認識するための正規表現を表します。
		/// </summary>
		private static readonly Regex tokenRecongnizer = new Regex( @"(?<=^(\$))(.*)" );

		/// <summary>
		///		先頭に「$」が付いた名前をトークンではなく、「$」付きの名前として認識するための正規表現を表します。
		/// </summary>
		private static readonly Regex antiTokenRecongnizer = new Regex( @"(?<=^(\\))(\$.*)" );

		/// <summary>
		///		<see cref="DirectoryGenerator">クラスからメッセージを受け取った時に実行するイベントハンドラーです。
		/// </summary>
		public EventHandler<MessageEventArgs> NotifyMessageRecieved;

		/// <summary>
		///		<see cref="DirectoryGenerator">クラスの新しいインスタンスを生成します。
		/// </summary>
		public DirectoryGenerator() { }

		/// <summary>
		///		メッセージを発行します。
		/// </summary>
		/// <param name="message">クラス利用側に送信するメッセージ</param>
		private void NotifyMessage( string message ) {
			NotifyMessageRecieved?.Invoke( this, new MessageEventArgs( message ) );
		}

		/// <summary>
		///		ディレクトリー構造を定義したXMLファイルを読み込みます。
		/// </summary>
		/// <param name="xmlPath">XMLファイルのパス</param>
		public void Load( string xmlPath ) {

			NotifyMessage( $"'{xmlPath}' をロードしています。" );
			XElement xml = XElement.Load( xmlPath );

			definitionFilePath = Path.GetDirectoryName( Path.GetFullPath( xmlPath ) );

			NotifyMessage( $"ディレクトリーの構成を読み込んでいます。" );
			directoryTree = new XElement( xml.Element( "directory" ) );

			NotifyMessage( $"トークンを読み込んでいます。" );
			// <tokens>ノードの<token>ノードのコレクションを取得します。
			tokenListMap = xml.Element( "tokens" ).Elements( "token" )
				.ToDictionary(
					// トークン（key属性値）をkeyに、
					tkey => tkey.Attribute( "key" ).Value,
					// トークンから置き換える文字列群（<token_value>ノードのvalue属性値のコレクション）を値に設定します。
					tval => tval.Elements( "token_value" )
								.Select( tv => tv.Attribute( "value" ).Value )
								.Distinct()
								.ToArray()
				);

			NotifyMessage( $"出力先のパスを読み込んでいます。" );
			// target_pathが未指定の場合、アプリのパスを設定します。
			targetRootPath = directoryTree.Attribute( "target_path" )?.Value ?? definitionFilePath;

			// nameが未指定の場合、nullを設定します。
			targetRootFolderName = directoryTree.Attribute( "name" )?.Value;

			NotifyMessage( $"'{xmlPath}' のロードを完了しました。" );
		}

		/// <summary>
		///		ディレクトリー構造を解析し、トークンを展開します。
		/// </summary>
		private void ExtractDirectoryTree() {
			NotifyMessage( $"ディレクトリー構成を解析しています。" );

			// 展開後のディレクトリー構造のXMLオブジェクトを作成します。
			extractedDirectoryTree = new XElement( directoryTree );
			// extractedDirectoryTreeの参照をコピーし、カレントノードを作成します。
			// ※カレントノード経由でXMLの内容を変更すると、extractedDirectoryTreeにも反映します。
			var curNode = extractedDirectoryTree;

			// 子ノードを探索済みフラグを表します。
			bool childNodeIsVisited = false;

			while( curNode != null ) {

				#region 子ノードの探索

				// カレントノードにて、未探索の子ノードがあるかどうか判別します。
				if( !childNodeIsVisited && curNode.HasElements ) {
					// 末っ子のノードにアクセスします。
					while( true ) {
						// 子ノードを取得し、種類を判別します。
						var childNode = curNode.FirstNode;
						// 子ノードが存在しない時、ループから抜けます。
						if( childNode == null ) {
							break;
						}
						// 子ノードが要素の時、カレントノードに設定します。
						else if( childNode is XElement ) {
							curNode = childNode as XElement;
						}
						// 子ノードが要素以外（コメントなど）の時、そのノードを削除します。
						else {
							childNode.Remove();
						}
					}
					continue;
				}
				// 子ノードを探索済みの場合、フラグをオフにします。
				else if( childNodeIsVisited ) {
					// 直下のサブディレクトリー内のフォルダー名やファイル名が重複していないかチェックします。
					if( curNode.Elements().GroupBy( node => node.Attribute( "name" ).Value ).Any( _ => _.Count() >= 2 ) ) {
						throw new Exception( $"'{curNode.Attribute( "name" ).Value}' フォルダーの中に、同じ名前のフォルダーまたはファイルが重複しています。" );
					}

					childNodeIsVisited = false;
				}

				#endregion

				#region トークンの展開

				var name = curNode.Attribute( "name" );
				if( name?.Value != null ) {
					// name属性の値がトークンであるかどうか判別します。
					if( tokenRecongnizer.IsMatch( name.Value ) ) {
						// 「$」以降の文字列を取り出します。
						var token = tokenRecongnizer.Match( name.Value ).Value;
						// トークンのあるノードの参照をコピーします。
						var tokenNode = curNode;
						// トークンが辞書に含まれているか判別します。
						if( tokenListMap.ContainsKey( token ) ) {
							// トークンに登録されているフォルダー名・ファイル名を展開します。
							foreach( var item in tokenListMap[token] ) {
								if( fileNameInvaildRecongnizer.IsMatch( item ) ) {
									throw new Exception( $"トークンに対応する名前 '{item}' に、ファイル名として使用できない文字（ \\ / : * ? \" < > | ）が含まれています。" );
								}
								// 展開後の名前をname属性にしたノードを追加します。
								curNode.AddAfterSelf( new XElement( tokenNode.Name, new XAttribute( "name", item ) ) );
								// 追加したノードにアクセスします。
								curNode = curNode.NextNode as XElement;
								// トークンノードに子ノードがあれば、追加したノードの子にコピーします。
								if( tokenNode.HasElements ) {
									curNode.Add( tokenNode.Elements() );
								}
							}
						}
						// トークンノードを削除します。
						tokenNode.Remove();
					}
					else {
						if( antiTokenRecongnizer.IsMatch( name.Value ) ) {
							// 先頭の「$」の前に付いている「\」を取り除きます。
							name.Value = antiTokenRecongnizer.Match( name.Value ).Value;
						}
						if( fileNameInvaildRecongnizer.IsMatch( name.Value ) ) {
							throw new Exception( $"名前 '{name.Value}' に、ファイル名として使用できない文字（ \\ / : * ? \" < > | ）が含まれています。" );
						}
					}
				}
				else {
					throw new Exception( $"{curNode.Name} ノードにname属性が存在しないか、属性名が無効です。" );
				}

				#endregion

				#region 次のノードの探索

				// 次のノードを判別します。
				while( true ) {
					var nextNode = curNode.NextNode;
					// 次のノードが存在しない時、親のノードに戻ります。
					if( nextNode == null ) {
						curNode = curNode.Parent;
						childNodeIsVisited = true;
						break;
					}
					// 次のノードが要素の時、カレントノードに設定します。
					else if( nextNode is XElement ) {
						curNode = nextNode as XElement;
						break;
					}
					// 次のノードが要素以外（コメントなど）の時、そのノードを削除します。
					else {
						nextNode.Remove();
					}
				}

				#endregion
			}

			NotifyMessage( $"ディレクトリー構成の解析が完了しました。" );
		}

		/// <summary>
		///		展開後のディレクトリー構造からディレクトリーを作成します。
		/// </summary>
		private void CreateDirectory() {

			try {
				NotifyMessage( $"ディレクトリーを作成しています。" );

				// ディレクトリーの出力先パスが存在するかどうか判別します。
				if( !Directory.Exists( targetRootPath ) ) {
					throw new Exception( $"出力先パス '{targetRootPath}' が存在しないか、ディレクトリー名が無効です。" );
				}

				// ディレクトリーの出力先パスにカレントディレクトリーを移動します。
				Directory.SetCurrentDirectory( targetRootPath );

				// ルートフォルダーを作成するかどうか判別します。
				if( targetRootFolderName != null ) {
					// ディレクトリーの出力先パスが既に存在するかどうか判別します。
					if( Directory.Exists( targetRootFolderName ) ) {
						throw new Exception( $"ルートフォルダー '{targetRootFolderName}' は出力先パスに既に存在しています。削除してからやり直してください。" );
					}
					// ルートフォルダーを作成し、カレントディレクトリーを移動します。
					Directory.CreateDirectory( targetRootFolderName );
					Directory.SetCurrentDirectory( targetRootFolderName );
				}
				else {
					// 出力先パスにファイルやフォルダーが存在するかどうか判別します。
					if( Directory.EnumerateDirectories( Directory.GetCurrentDirectory() ).Any() ||
						Directory.EnumerateFiles( Directory.GetCurrentDirectory() ).Any() ||
						Directory.EnumerateFileSystemEntries( Directory.GetCurrentDirectory() ).Any() ) {
						throw new Exception( $"出力先パス '{targetRootPath}' 内に別のファイルやフォルダーが存在しています。それらを削除するか、ディレクトリーを格納するルートフォルダーの作成を検討してください。" );
					}
				}

				// SAXパーサーを使って、展開後のXMLツリーを読み込み、ディレクトリーを作成していきます。
				using( var reader = XmlReader.Create( new StringReader( extractedDirectoryTree.ToString() ) ) ) {
					while( reader.Read() ) {
						switch( reader.NodeType ) {
							case XmlNodeType.Element:       // 要素のノード
								// 現在のノードの子ノードから空であるかどうかの情報を取得します。
								// ※ここで値を保持するのは、MoveToAttributeメソッドの実行後では、値を正しく取得できないからです。
								var isEmptyElement = reader.IsEmptyElement;

								// 要素名を判別します。
								var type = reader.Name;
								if( reader.MoveToAttribute( "name" ) ) {
									// フォルダー用のノードの時、name属性に指定した名前のフォルダーを作成し、カレントディレクトリーを移動します。
									if( type == "folder" ) {
										var path = reader.Value;
										NotifyMessage( $"ディレクトリー '{path}' を作成しています。( 出力先 : {Directory.GetCurrentDirectory()} )" );
										Directory.CreateDirectory( path );
										Directory.SetCurrentDirectory( path );
										// 子ノードが空の時、1つ親のフォルダーに移動します。
										if( isEmptyElement ) {
											Directory.SetCurrentDirectory( "..\\" );
										}
									}
									// ファイル用のノードの時、name属性に指定した名前のファイルをコピーします。
									else if( type == "file" ) {
										var sourceFilePath = reader.Value;
										var targetFilePath = Path.GetFileName( sourceFilePath );
										// 移動元のファイルのパスを生成します。
										if( !Path.IsPathRooted( sourceFilePath ) ) {
											sourceFilePath = $"{definitionFilePath}\\{sourceFilePath}";
										}
										NotifyMessage( $"ファイル '{targetFilePath}' をコピーしています。( 出力先 : {Directory.GetCurrentDirectory()} )" );
										File.Copy( sourceFilePath, targetFilePath );
									}
								}
								break;
							case XmlNodeType.EndElement:        // 要素の終了ノード
								// フォルダー用ノードの時、1つ親のフォルダーに移動します。
								if( reader.Name == "folder" ) {
									Directory.SetCurrentDirectory( "..\\" );
								}
								break;
						}
					}
				}
			}
			finally {
				// アプリのパスにカレントディレクトリーを移動します。
				Directory.SetCurrentDirectory( appPath );
			}

			NotifyMessage( $"ディレクトリーの作成が完了しました。" );
		}

		/// <summary>
		///		読み込んだ定義ファイルに基づき、ディレクトリーを作成します。
		/// </summary>
		public void GenerateDirectory() {
			ExtractDirectoryTree();
			CreateDirectory();
		}

	}
}
