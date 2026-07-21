// works/works-data.js
//
// 作品を追加するときは、下の該当カテゴリーの works 配列に
// { title: "作品タイトル", publishedAt: "年-月-日", youtubeUrl: "YouTube動画URL" }
// をコピーして追加するだけです。タイトル・投稿日・URLを増やせば、
// 作品一覧ページのカードとサムネイルは自動で増えます。
//
// ▼ 記入例（コピー用のお手本です。このままではカードとして表示されません）
// {
//   title: "サンプル作品タイトル",
//   publishedAt: "2026-01-23",
//   youtubeUrl: "https://youtu.be/xxxxxxxxxxx"
// },

const worksData = [
  {
    id: "utattemita",
    name: "歌ってみた",
    description: "Arrange coverした歌ってみた作品",
    works: [
      {
        title: "脱法ロックｳﾀｯﾀ (Arrange cover)",
        publishedAt: "2021-10-17",
        youtubeUrl: "https://youtu.be/XRIndSupS3A",
      },
      {
        title: "ダンスロボットダンスｳﾀｯﾀ (Arrange cover)",
        publishedAt: "2022-04-26",
        youtubeUrl: "https://youtu.be/l2e99tq3o_8",
      },
      {
        title: "チュルリラ・チュルリラ・ダッダッダ！ｳﾀｯﾀ (Arrange cover)",
        publishedAt: "2022-10-11",
        youtubeUrl: "https://youtu.be/fGeiKZKWiYo",
      },
      {
        title: "かなしみのなみにおぼれるｳﾀｯﾀ (Arrange cover)",
        publishedAt: "2022-12-11",
        youtubeUrl: "https://youtu.be/V5758wiJebI",
      },
      {
        title: "【1月～12月】2022年のボカロ曲ｳﾀｯﾀ【アレンジメドレー】",
        publishedAt: "2023-01-10",
        youtubeUrl: "https://youtu.be/ApFbC5bvf-A",
      },
      {
        title: "ラヴィｳﾀｯﾀ (Arrange cover)",
        publishedAt: "2023-04-22",
        youtubeUrl: "https://youtu.be/ZYuaQ8fXNbk",
      },
      {
        title: "歌った弾いた描いたまとめ+α",
        publishedAt: "2023-07-15",
        youtubeUrl: "https://youtu.be/uK8ucuPYT4A",
      },
      {
        title: "酔いどれ知らずｳﾀｯﾀ (Arrange cover)",
        publishedAt: "2023-09-01",
        youtubeUrl: "https://youtu.be/cJ1kZ6NaYw4",
      },
      {
        title: "KINGｳﾀｯﾀ (Arrange cover)",
        publishedAt: "2024-04-13",
        youtubeUrl: "https://youtu.be/0kB3M3uBTG4",
      },
      {
        title: "劣等上等 -Arrange cover-/ ばぶちゃん革命(NG。×HALKA)",
        publishedAt: "2024-11-22",
        youtubeUrl: "https://youtu.be/2kCT91pDb00",
      },
      {
        title: "【アレンジメドレー】2024年のボカロ曲ｳﾀｯﾀ【ばぶちゃん革命】",
        publishedAt: "2025-02-05",
        youtubeUrl: "https://youtu.be/Q8kAwOBDnrc",
      },
      {
        title: "新人類 -Arrange cover-/ ばぶちゃん革命(NG。×HALKA)",
        publishedAt: "2025-03-20",
        youtubeUrl: "https://youtu.be/5G9_KFCqZqM",
      },
      {
        title: "リモコン -Arrange cover-/ ばぶちゃん革命(NG。×HALKA)",
        publishedAt: "2025-07-14",
        youtubeUrl: "https://youtu.be/upNA-rYkkb0",
      },
      {
        title: "メズマライザーｳﾀｯﾀ (Arrange cover)",
        publishedAt: "2025-08-05",
        youtubeUrl: "https://youtu.be/RFQw7HejNZ0",
      },
      {
        title: "ダイダイダイダイダイキライｳﾀｯﾀ (Arrange cover)",
        publishedAt: "2025-09-26",
        youtubeUrl: "https://youtu.be/eAgSVaWBiNU",
      },
      {
        title: "狂乱 Hey Kids!!ｳﾀｯﾀ (Arrange cover)",
        publishedAt: "2025-11-11",
        youtubeUrl: "https://youtu.be/qoNbJLaE69M",
      },
      {
        title: "いますぐ輪廻ｳﾀｯﾀ (Arrange cover)",
        publishedAt: "2025-12-08",
        youtubeUrl: "https://youtu.be/ixThfAhCLSU",
      },
      {
        title: "パンダヒーローｳﾀｯﾀ (Arrange cover)",
        publishedAt: "2026-01-23",
        youtubeUrl: "https://youtu.be/FM5O_I8yl-4",
      },
      {
        title: "アナタサマｳﾀｯﾀ (Arrange cover)",
        publishedAt: "2026-03-06",
        youtubeUrl: "https://youtu.be/zbWYzK2D7r8",
      },
      {
        title: "アイ・アイ・アｳﾀｯﾀ (Arrange cover)",
        publishedAt: "2026-05-04",
        youtubeUrl: "https://youtu.be/GPo3hph2QuE",
      },
      {
        title: "IRIS OUTｳﾀｯﾀ (Arrange cover)",
        publishedAt: "2026-07-03",
        youtubeUrl: "https://youtu.be/jQBH_FHq0iI",
      },
    ],
  },
  {
    id: "hiragana",
    name: "ひらがな一文字シリーズ",
    description: "ひらがな一文字ずつをテーマに制作している楽曲シリーズ。",
    works: [
      {
        title: "あ / 鏡音リン",
        publishedAt: "2021-07-20",
        youtubeUrl: "https://youtu.be/ys5dBTKxdgw",
      },
      {
        title: "い / 鏡音リン",
        publishedAt: "2021-07-23",
        youtubeUrl: "https://youtu.be/AkfLhKfjmfk",
      },
      {
        title: "う / 鏡音リン",
        publishedAt: "2021-07-31",
        youtubeUrl: "https://youtu.be/GwEZ93BV1Go",
      },
      {
        title: "え / 鏡音リン&鏡音レン",
        publishedAt: "2021-08-11",
        youtubeUrl: "https://youtu.be/BD1B6sV-I6Q",
      },
      {
        title: "お / 鏡音リン",
        publishedAt: "2021-08-22",
        youtubeUrl: "https://youtu.be/UGs4j5fiW0Q",
      },
      {
        title: "か / 鏡音リン",
        publishedAt: "2021-09-04",
        youtubeUrl: "https://youtu.be/gBRIZ9-J1Dk",
      },
      {
        title: "き / 鏡音リン",
        publishedAt: "2021-10-15",
        youtubeUrl: "https://youtu.be/owLIY6eFk94",
      },
      {
        title: "く / 鏡音リン",
        publishedAt: "2021-09-20",
        youtubeUrl: "https://youtu.be/dGd7WmahncE",
      },
      {
        title: "け / 鏡音リン&鏡音レン",
        publishedAt: "2021-10-24",
        youtubeUrl: "https://youtu.be/_9Iz-kb90zA",
      },
      {
        title: "こ / 鏡音リン",
        publishedAt: "2021-11-12",
        youtubeUrl: "https://youtu.be/ZvxwADkhooU",
      },
      {
        title: "さ / 鏡音リン",
        publishedAt: "2021-12-04",
        youtubeUrl: "https://youtu.be/9x98vwejAOk",
      },
      {
        title: "し / 鏡音リン",
        publishedAt: "2021-12-27",
        youtubeUrl: "https://youtu.be/GOLnG4txvFs",
      },
      {
        title: "す / 鏡音リン",
        publishedAt: "2022-05-09",
        youtubeUrl: "https://youtu.be/jkCsCxp0cZ8",
      },
      {
        title: "せ / 鏡音リン",
        publishedAt: "2022-01-30",
        youtubeUrl: "https://youtu.be/bYZq9Jmo4r4",
      },
      {
        title: "そ / 鏡音リン",
        publishedAt: "2022-04-23",
        youtubeUrl: "https://youtu.be/cdsPb_a1gJI",
      },
      {
        title: "た / 鏡音リン",
        publishedAt: "2022-03-20",
        youtubeUrl: "https://youtu.be/gqai58cdi04",
      },
      {
        title: "ち / 鏡音リン",
        publishedAt: "2022-06-17",
        youtubeUrl: "https://youtu.be/M0FJCETc_28",
      },
      {
        title: "つ / 鏡音リン&鏡音レン",
        publishedAt: "2022-07-30",
        youtubeUrl: "https://youtu.be/ZISe-1xkIMs",
      },
      {
        title: "て / 鏡音レン",
        publishedAt: "2022-09-27",
        youtubeUrl: "https://youtu.be/vJiCxbHnFcc",
      },
      {
        title: "と / 鏡音リン",
        publishedAt: "2022-10-28",
        youtubeUrl: "https://youtu.be/nZRCRRxcg60",
      },
      {
        title: "な / 鏡音リン",
        publishedAt: "2022-11-28",
        youtubeUrl: "https://youtu.be/wH12KCuvQIw",
      },
      {
        title: "に / 鏡音リン",
        publishedAt: "2022-10-08",
        youtubeUrl: "https://youtu.be/ampeE-O7mUo",
      },
      {
        title: "ぬ / 鏡音レン",
        publishedAt: "2023-02-22",
        youtubeUrl: "https://youtu.be/sshlHqSZZFs",
      },
      {
        title: "ね / 鏡音リン&鏡音レン",
        publishedAt: "2022-12-27",
        youtubeUrl: "https://youtu.be/SU5OoZ46lOU",
      },
      {
        title: "の / 鏡音リン",
        publishedAt: "2023-03-19",
        youtubeUrl: "https://youtu.be/A36lWyK2ZxA",
      },
      {
        title: "は / 鏡音リン",
        publishedAt: "2023-05-28",
        youtubeUrl: "https://youtu.be/HujZpZo-HU4",
      },
      {
        title: "ひ / 鏡音リン",
        publishedAt: "2023-06-28",
        youtubeUrl: "https://youtu.be/vzn2nT-0VnU",
      },
      {
        title: "ふ / 鏡音リン",
        publishedAt: "2023-07-25",
        youtubeUrl: "https://youtu.be/ixDUjQ0kfzk",
      },
      {
        title: "へ / 鏡音レン",
        publishedAt: "2023-08-05",
        youtubeUrl: "https://youtu.be/NRLA7McvmV8",
      },
      {
        title: "ほ / 鏡音リン&鏡音レン",
        publishedAt: "2023-12-27",
        youtubeUrl: "https://youtu.be/rgS7bmUb11c",
      },
      {
        title: "ま / 鏡音リン",
        publishedAt: "2024-02-23",
        youtubeUrl: "https://youtu.be/XyB3PIJMOoI",
      },
      {
        title: "み / 鏡音リン",
        publishedAt: "2024-05-29",
        youtubeUrl: "https://youtu.be/Od5o4OwUv7s",
      },
      {
        title: "む / 鏡音レン",
        publishedAt: "2024-09-18",
        youtubeUrl: "https://youtu.be/_va68tqsUr8",
      },
      {
        title: "め / 鏡音リン",
        publishedAt: "2024-11-08",
        youtubeUrl: "https://youtu.be/9CP_Ka5tdbA",
      },
      {
        title: "も / 鏡音リン",
        publishedAt: "2024-12-27",
        youtubeUrl: "https://youtu.be/CvYn-vrXMj0",
      },
      {
        title: "や / 鏡音レン",
        publishedAt: "2025-02-22",
        youtubeUrl: "https://youtu.be/BxsEZQqoZq8",
      },
      {
        title: "ゆ / 鏡音レン",
        publishedAt: "2025-06-14",
        youtubeUrl: "https://youtu.be/WsU-GzWwv5M",
      },
      {
        title: "よ / 鏡音レン",
        publishedAt: "2025-10-05",
        youtubeUrl: "https://youtu.be/BQZbR8k9Ujg",
      },
      {
        title: "ら / 鏡音リン",
        publishedAt: "2026-01-15",
        youtubeUrl: "https://youtu.be/Mdc_Hw7PrcE",
      },
    ],
  },
  {
    id: "direction",
    name: "方向シリーズ",
    description: "前・後・右・左・上・下などの向きをテーマにした楽曲シリーズ。",
    works: [
      {
        title: "まえがみえない！ / 鏡音リン&鏡音レン",
        publishedAt: "2025-10-15",
        youtubeUrl: "https://youtu.be/B7wTgKAix5U",
      },
      {
        title: "うしろみちゃだめ！ / 鏡音リン&鏡音レン",
        publishedAt: "2025-12-27",
        youtubeUrl: "https://youtu.be/HI2b-OGtXKg",
      },
      {
        title: "ひだりがわのてんし！ / 鏡音レン",
        publishedAt: "2026-03-30",
        youtubeUrl: "https://youtu.be/4RifNQvVt7A",
      },
      {
        title: "うえをむいてさけぼう！ /  鏡音リン&鏡音レン",
        publishedAt: "2026-05-29",
        youtubeUrl: "https://youtu.be/9-LmCNV1c2U",
      },
      {
        title: "みぎがわのあくま！ / 鏡音リン",
        publishedAt: "2026-07-17",
        youtubeUrl: "https://youtu.be/qLWV98ls19o",
      },
    ],
  },
  {
    id: "sonota",
    name: "その他",
    description: "はぐれもの",
    works: [
      {
        title: "わぁっ！ / 鏡音リン",
        publishedAt: "2026-02-27",
        youtubeUrl: "https://youtu.be/wn3v_R_DnDM",
      },
    ],
  },
];
