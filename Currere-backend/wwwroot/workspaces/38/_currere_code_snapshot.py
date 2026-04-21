def plot_histogram_evolution(hedef='exam_score'):
    fig, eksenler = plt.subplots(nrows=1, ncols=4, figsize=(24, 5))

    # Create temporary copies and lowercase column names for consistent plotting
    df_temp = df.copy()
    df_temp.columns = df_temp.columns.str.lower()

    kirli_veri_temp = kirli_veri.copy()
    # Ensure kirli_veri columns are stripped and lowercased to handle potential spaces/case issues
    kirli_veri_temp.columns = kirli_veri_temp.columns.str.strip().str.lower()

    temiz_veri_temp = temiz_veri.copy() # Its columns should already be lowercased
    onisleme_veri_temp = onisleme_veri.copy() # Its columns should already be lowercased

    datasets = [df_temp, kirli_veri_temp, temiz_veri_temp, onisleme_veri_temp]
    titles = ['1. Ham Veri (Orjinal)', '2. Kirli Veri (Bozulmuş)',
              '3. Temiz Veri (Toparlanmış)', '4. Önişlenmiş Veri (Ölçekli)']
    colors = ['skyblue', 'salmon', 'mediumseagreen', 'purple']

    for i, data in enumerate(datasets):
        sns.histplot(data=data, x=hedef, kde=True, color=colors[i], ax=eksenler[i])
        eksenler[i].set_title(titles[i], fontweight='bold')
        if i > 0: eksenler[i].set_ylabel('')

    plt.suptitle(f'{hedef.upper()} Değişkeninin İşlem Adımlarına Göre Evrimi', fontsize=16, y=1.05, fontweight='bold')
    plt.tight_layout()
    plt.show()

# Hücrede çalıştır
plot_histogram_evolution()