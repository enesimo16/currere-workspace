#ÖZELLİK SEÇİMİ VE ISI HARİTASI(KORELASYON)
#şimdi korelasyon yapacağız.
#bunu yapma sebebimiz hangi değişkenin targetımız(exam_score) ile ne derecede bağlı oduğunu sayısal olarak görmek.

#Korelasyonda  kırmızılar negaif ilişkiyi, yeşiller pozitif ilişkiyi temsil eder.
#buradan yola çıkarak attendance ile targetımız(exam_score) arasında pozitif bir ilişki olup, attendance ın artışı exam_score'u oldukça arttıracaktır yorumu yapılabilir.
#ÖZELLİK SEÇİMİ aşamasında korelasyonu oldukça düşün olan (-0.10 ile 0.10 arasındakiler) targetımıza yüksek etkide bulunmadığından ötürü silinebilir.
#bu durumda sleep_hours silinse dahi elde edilecek analizlerde çok fark olmayacaktır.

# 3. ŞİMDİ EN ÖNEMLİ GRAFİK: FİNAL ISI HARİTASI
import matplotlib.pyplot as plt
import seaborn as sns

plt.figure(figsize=(20, 12))
# Korelasyonu hesapla ve çizdir
sns.heatmap(onisleme_veri_final.corr(), annot=True, cmap='RdYlGn', fmt=".2f", linewidths=0.5)

plt.title(" Bütünleşik Özellik Seçimi Analizi (Korelasyon)")
plt.xticks(rotation=90)
plt.show()

# 4. Sınav Puanı Üzerindeki "Gerçek" Etki Listesi
print("\nSınav Puanını (Exam Score) En Çok Etkileyen İlk 5 Faktör:")
print(onisleme_veri_final.corr()['exam_score'].sort_values(ascending=False).iloc[1:6])