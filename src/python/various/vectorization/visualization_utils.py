import umap
import altair as alt
import pandas as pd
from sklearn.manifold import TSNE
import matplotlib.pyplot as plt
import matplotlib
import numpy as np

def plot_embeddings(sentences, embeddings):
    panda_sentences = pd.DataFrame({'text':sentences})

    # UMAP reduces dimension to a plottable 2DE
    reducer = umap.UMAP(n_neighbors=5)
    umap_embeds = reducer.fit_transform(embeddings)
    
    # create a dataframe with the umap embeddings and the corresponding sentences
    df_plot = pd.DataFrame({'x': umap_embeds[:, 0], 'y': umap_embeds[:, 1], 'text': panda_sentences['text']})
    
    # create a column to specify the color of each point
    colors = ['blue']*10 + ['red']*10 + ['green']*10

    df_plot['color'] = colors
    
    # create the interactive scatter plot with labels
    return alt.Chart(df_plot, width=1100, height=600).mark_circle(size=60).encode(
        x='x',
        y='y',
        color=alt.Color('color', scale=None),
        tooltip=['text']
    )

def openai_plot_2D(sentences, embeddings, show_labels=True):
    matrix = pd.DataFrame(embeddings)

    # Create a t-SNE model and transform the data
    tsne = TSNE(n_components=2, perplexity=15, random_state=42, init='random', learning_rate=200)
    vis_dims = tsne.fit_transform(matrix)

    colors = ["red", "blue", "green"]
    x = [x for x, y in vis_dims]
    y = [y for x, y in vis_dims]
    color_indices = matrix.index.values

    colormap = matplotlib.colors.ListedColormap(colors)

    if (show_labels):
        figsize = (12, 8)
    else:
        figsize = (6, 3)
    fig, ax = plt.subplots(figsize=figsize)  # Set the figure size to 12x12
    scatter = ax.scatter(x, y, c=color_indices, cmap=colormap, alpha=0.3)
    plt.title("Embedding in 2d")

    # Add legends based on sentences
    if show_labels:
        for i, sentence in enumerate(sentences):
            plt.text(x[i], y[i], sentence, fontsize=8, ha='center', va='center')

    plt.show()
    print (plt.isinteractive())
