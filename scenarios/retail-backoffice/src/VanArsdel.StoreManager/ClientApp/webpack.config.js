const path = require('path');
const HtmlWebpackPlugin = require('html-webpack-plugin');

// Output goes to the host's wwwroot/app; the host serves it and adds /config.json.
module.exports = (env, argv) => ({
  entry: './src/index.jsx',
  output: {
    path: path.resolve(__dirname, '../wwwroot/app'),
    filename: argv.mode === 'production' ? 'js/[name].[contenthash:8].js' : 'js/[name].js',
    publicPath: '/',
    clean: true,
  },
  resolve: { extensions: ['.js', '.jsx'] },
  module: {
    rules: [
      {
        test: /\.jsx?$/,
        exclude: /node_modules/,
        use: {
          loader: 'babel-loader',
          options: { presets: ['@babel/preset-env', ['@babel/preset-react', { runtime: 'automatic' }]] },
        },
      },
      { test: /\.css$/, use: ['style-loader', 'css-loader'] },
    ],
  },
  plugins: [new HtmlWebpackPlugin({ template: './public/index.html' })],
  devtool: argv.mode === 'production' ? false : 'eval-source-map',
  performance: { hints: false },
  devServer: {
    port: 3000,
    historyApiFallback: true,
    proxy: { '/config.json': 'http://localhost:5211' },
  },
});
